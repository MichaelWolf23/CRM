using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

// --- ИНТЕРФЕЙСЫ (КОНТРАКТЫ) ---

public interface IEntity
{
    int Id { get; }
}

public interface IStorage<T>
{
    Task SaveAsync(List<T> items);
    List<T> Load();
}

public interface IRepository<T> where T : class, IEntity
{
    IEnumerable<T> GetAll();
    T GetById(int id);
    void Add(T entity);
    Task SaveAsync();
    int GetNextId();
}

public interface IClientRepository : IRepository<Client> { }
public interface IOrderRepository : IRepository<Order> { }

public interface IClientReader
{
    IEnumerable<Client> GetAllClients();
}

public interface IClientWriter
{
    Client AddClient(string name, string email);
}

public interface IOrderReader
{
    IEnumerable<Order> GetAllOrders();
}

public interface IOrderWriter
{
    Order AddOrderForClient(int clientId, string description, decimal amount);
}

/// <summary>
/// Интерфейс для Фасада, упрощающего работу с CRM.
/// </summary>
public interface ICrmFacade
{
    void RegisterNewClientWithFirstOrder(string clientName, string clientEmail, string orderDescription, decimal orderAmount);
}

/// <summary>
/// Интерфейс "Строителя" для отчетов
/// </summary>
public interface IReportBuilder
{
    void Reset();
    void BuildTitle(string title);
    void BuildClientSection();
    void BuildOrderSection();
    void BuildSummary();
    Report GetResult();
}


// --- МОДЕЛИ ДАННЫХ ---
public record Client(int Id, string Name, string Email, DateTime CreatedAt) : IEntity;
public record Order(int Id, int ClientId, string Description, decimal Amount, DateOnly DueDate) : IEntity;

// --- СЛОЙ ХРАНЕНИЯ ДАННЫХ (STORAGE) ---
public class JsonFileStorage<T> : IStorage<T>
{
    private readonly string _filePath;
    public JsonFileStorage(string filePath) { _filePath = filePath; }

    public List<T> Load()
    {
        if (!File.Exists(_filePath)) return new List<T>();
        var json = File.ReadAllText(_filePath);
        return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
    }

    public async Task SaveAsync(List<T> items)
    {
        var json = JsonConvert.SerializeObject(items, Newtonsoft.Json.Formatting.Indented);
        await File.WriteAllTextAsync(_filePath, json);
    }
}

// --- СЛОЙ ДОСТУПА К ДАННЫМ (РЕПОЗИТОРИИ) ---
public abstract class BaseRepository<T> : IRepository<T> where T : class, IEntity
{
    protected List<T> _items;
    private readonly IStorage<T> _storage;

    protected BaseRepository(IStorage<T> storage)
    {
        _storage = storage;
        _items = _storage.Load();
    }

    public void Add(T entity) => _items.Add(entity);
    public IEnumerable<T> GetAll() => _items;
    public abstract T GetById(int id);

    public async Task SaveAsync()
    {
        await _storage.SaveAsync(_items);
    }

    public int GetNextId()
    {
        return _items.Any() ? _items.Max(e => e.Id) + 1 : 1;
    }
}

public class ClientRepository : BaseRepository<Client>, IClientRepository
{
    public ClientRepository(IStorage<Client> storage) : base(storage) { }
    public override Client GetById(int id) => _items.FirstOrDefault(c => c.Id == id);
}

public class OrderRepository : BaseRepository<Order>, IOrderRepository
{
    public OrderRepository(IStorage<Order> storage) : base(storage) { }
    public override Order GetById(int id) => _items.FirstOrDefault(o => o.Id == id);
}

// --- СЛОЙ БИЗНЕС-ЛОГИКИ (СЕРВИС) ---
public sealed class CrmService : IClientReader, IClientWriter, IOrderReader, IOrderWriter
{
    private readonly IClientRepository _clientRepository;
    private readonly IOrderRepository _orderRepository;
    public event Action<Client> ClientAdded;

    private static readonly Lazy<CrmService> lazy = new Lazy<CrmService>(() =>
    {
        var clientStorage = new JsonFileStorage<Client>("clients.json");
        var orderStorage = new JsonFileStorage<Order>("orders.json");
        var clientRepo = new ClientRepository(clientStorage);
        var orderRepo = new OrderRepository(orderStorage);
        return new CrmService(clientRepo, orderRepo);
    });

    public static CrmService Instance => lazy.Value;

    private CrmService(IClientRepository clientRepository, IOrderRepository orderRepository)
    {
        _clientRepository = clientRepository;
        _orderRepository = orderRepository;
    }

    public Client AddClient(string name, string email)
    {
        var nextId = _clientRepository.GetNextId();
        var client = new Client(nextId, name, email, DateTime.Now);
        _clientRepository.Add(client);
        _clientRepository.SaveAsync();
        ClientAdded?.Invoke(client);
        return client;
    }

    public Order AddOrderForClient(int clientId, string description, decimal amount)
    {
        var nextId = _orderRepository.GetNextId();
        var order = new Order(nextId, clientId, description, amount, DateOnly.FromDateTime(DateTime.Now.AddDays(14)));
        _orderRepository.Add(order);
        _orderRepository.SaveAsync();
        return order;
    }

    public IEnumerable<Client> GetAllClients() => _clientRepository.GetAll();
    public IEnumerable<Order> GetAllOrders() => _orderRepository.GetAll();
}

// --- ФАСАД ---
public class CrmFacade : ICrmFacade
{
    private readonly IClientWriter _clientWriter;
    private readonly IOrderWriter _orderWriter;

    public CrmFacade(IClientWriter clientWriter, IOrderWriter orderWriter)
    {
        _clientWriter = clientWriter;
        _orderWriter = orderWriter;
    }

    public void RegisterNewClientWithFirstOrder(string clientName, string clientEmail, string orderDescription, decimal orderAmount)
    {
        Console.WriteLine("\n[Facade] Выполняется сложный сценарий...");
        var client = _clientWriter.AddClient(clientName, clientEmail);
        _orderWriter.AddOrderForClient(client.Id, orderDescription, orderAmount);
        Console.WriteLine("[Facade] Сценарий успешно завершен.");
    }
}

// --- ПАТТЕРН СТРОИТЕЛЬ ---
public class Report
{
    private readonly List<string> _parts = new();
    public void AddPart(string part) => _parts.Add(part);
    public void DisplayReport()
    {
        Console.WriteLine("\n--- НАЧАЛО ОТЧЕТА ---");
        foreach (var part in _parts) Console.WriteLine(part);
        Console.WriteLine("--- КОНЕЦ ОТЧЕТА ---\n");
    }
}

public class SalesReportBuilder : IReportBuilder
{
    private Report _report;
    private readonly IClientReader _clientReader;
    private readonly IOrderReader _orderReader;

    public SalesReportBuilder(IClientReader clientReader, IOrderReader orderReader)
    {
        _clientReader = clientReader;
        _orderReader = orderReader;
        Reset();
    }

    public void Reset() => _report = new Report();
    public void BuildTitle(string title) => _report.AddPart($"====== {title.ToUpper()} ======");
    public void BuildClientSection()
    {
        _report.AddPart("\n--- Секция клиентов ---");
        var clients = _clientReader.GetAllClients();
        foreach (var client in clients) _report.AddPart(client.ToString());
    }
    public void BuildOrderSection()
    {
        _report.AddPart("\n--- Секция заказов ---");
        var orders = _orderReader.GetAllOrders();
        foreach (var order in orders) _report.AddPart(order.ToString());
    }
    public void BuildSummary()
    {
        _report.AddPart("\n--- Итоговая сводка ---");
        _report.AddPart($"Всего клиентов: {_clientReader.GetAllClients().Count()}");
        _report.AddPart($"Всего заказов: {_orderReader.GetAllOrders().Count()}");
    }
    public Report GetResult() => _report;
}

public class ReportDirector
{
    public void BuildFullReport(IReportBuilder builder)
    {
        builder.Reset();
        builder.BuildTitle("Полный отчет по продажам");
        builder.BuildClientSection();
        builder.BuildOrderSection();
        builder.BuildSummary();
    }
}

// --- КОМПОНЕНТЫ UI И УВЕДОМЛЕНИЙ ---
public class Notifier
{
    public void OnClientAdded(Client client)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Уведомление]: Добавлен новый клиент '{client.Name}'");
        Console.ResetColor();
    }
}

public class ConsoleUI
{
    private readonly ICrmFacade _crmFacade;
    // Для демонстрации Строителя нам нужны ридеры
    private readonly IClientReader _clientReader;
    private readonly IOrderReader _orderReader;

    public ConsoleUI(ICrmFacade crmFacade, IClientReader clientReader, IOrderReader orderReader)
    {
        _crmFacade = crmFacade;
        _clientReader = clientReader;
        _orderReader = orderReader;
    }

    public void Run()
    {
        Console.WriteLine("--- Демонстрация Фасада ---");
        _crmFacade.RegisterNewClientWithFirstOrder(
            "Сергей Павлов",
            "sergey@example.com",
            "Консультация по архитектуре",
            15000);

        Console.WriteLine("\n--- Демонстрация Строителя ---");
        var builder = new SalesReportBuilder(_clientReader, _orderReader);
        var director = new ReportDirector();
        director.BuildFullReport(builder);
        Report report = builder.GetResult();
        report.DisplayReport();

        Console.ReadLine();
    }
}

// --- ТОЧКА ВХОДА (КОРЕНЬ КОМПОЗИЦИИ) ---
public class Program
{
    public static void Main(string[] args)
    {
        var crmService = CrmService.Instance;
        var notifier = new Notifier();
        var facade = new CrmFacade(crmService, crmService);
        var ui = new ConsoleUI(facade, crmService, crmService);

        crmService.ClientAdded += notifier.OnClientAdded;

        ui.Run();
    }
}



