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

public interface IClientSearchStrategy
{
    bool IsMatch(Client client);
}

// Новые, сфокусированные интерфейсы для CrmService
public interface IClientReader
{
    IEnumerable<Client> GetAllClients();
    IEnumerable<Client> FindClients(IClientSearchStrategy strategy);
}

public interface IClientWriter
{
    Client AddClient(string name, string email);
}

public interface IOrderReader
{
    IEnumerable<Order> GetAllOrders();
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


// --- СТРАТЕГИИ ПОИСКА ---
public class SearchClientsByNameStrategy : IClientSearchStrategy
{
    private readonly string _name;
    public SearchClientsByNameStrategy(string name) { _name = name.ToLower(); }
    public bool IsMatch(Client client) => client.Name.ToLower().Contains(_name);
}

public class SearchClientsByEmailStrategy : IClientSearchStrategy
{
    private readonly string _emailDomain;
    public SearchClientsByEmailStrategy(string emailDomain) { _emailDomain = emailDomain.ToLower(); }
    public bool IsMatch(Client client) => client.Email.ToLower().EndsWith(_emailDomain);
}


// --- СЛОЙ БИЗНЕС-ЛОГИКИ (СЕРВИС) ---
public sealed class CrmService : IClientReader, IClientWriter, IOrderReader
{
    private readonly IClientRepository _clientRepository;
    private readonly IOrderRepository _orderRepository;
    public event Action<Client> ClientAdded;

    private static readonly Lazy<CrmService> lazy = new Lazy<CrmService>(() =>
    {
        var clientStorage = new JsonFileStorage<Client>("clients.json");
        var orderStorage = new JsonFileStorage<Order>("orders.json");

        var realClientRepo = new ClientRepository(clientStorage);
        var clientRepo = new ClientRepositoryProxy(realClientRepo);
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
    public IEnumerable<Client> FindClients(IClientSearchStrategy strategy)
    {
        return _clientRepository.GetAll().Where(client => strategy.IsMatch(client));
    }
}


// --- КОМПОНЕНТЫ UI И УВЕДОМЛЕНИЙ ---
public class Notifier
{
    public void OnClientAdded(Client client)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Уведомление]: Добавлен новый клиент '{client.Name}' с Email: {client.Email}");
        Console.ResetColor();
    }
}


// --- ГЕНЕРАТОРЫ ОТЧЕТОВ ---
public abstract class BaseReportGenerator
{
    protected readonly IClientReader _clientReader;
    protected readonly IOrderReader _orderReader;

    protected BaseReportGenerator(IClientReader clientReader, IOrderReader orderReader)
    {
        _clientReader = clientReader;
        _orderReader = orderReader;
    }
    public void Generate()
    {
        GenerateHeader();
        GenerateBody();
        GenerateFooter();
    }
    protected virtual void GenerateHeader()
    {
        Console.WriteLine("===================================");
        Console.WriteLine("        ОТЧЕТ ПО СИСТЕМЕ CRM       ");
        Console.WriteLine("===================================");
    }
    protected virtual void GenerateFooter()
    {
        Console.WriteLine("-----------------------------------");
        Console.WriteLine($"Отчет сгенерирован: {DateTime.Now}");
        Console.WriteLine("===================================");
    }
    protected abstract void GenerateBody();
}

public class ClientListReport : BaseReportGenerator
{
    public ClientListReport(IClientReader clientReader, IOrderReader orderReader)
        : base(clientReader, orderReader) { }

    protected override void GenerateBody()
    {
        Console.WriteLine("\n--- Список всех клиентов ---");
        var clients = _clientReader.GetAllClients();
        foreach (var client in clients)
        {
            Console.WriteLine($"ID: {client.Id}, Имя: {client.Name}, Email: {client.Email}");
        }
    }
}

public class ClientOrdersReport : BaseReportGenerator
{
    public ClientOrdersReport(IClientReader clientReader, IOrderReader orderReader)
        : base(clientReader, orderReader) { }

    protected override void GenerateBody()
    {
        Console.WriteLine("\n--- Детальный отчет по заказам клиентов ---");
        var clients = _clientReader.GetAllClients();
        var allOrders = _orderReader.GetAllOrders();

        foreach (var client in clients)
        {
            Console.WriteLine($"\nКлиент: {client.Name} (ID: {client.Id})");
            var clientOrders = allOrders.Where(o => o.ClientId == client.Id);
            if (clientOrders.Any())
            {
                foreach (var order in clientOrders)
                {
                    Console.WriteLine($"  - Заказ #{order.Id}: {order.Description} на сумму {order.Amount:C}");
                }
            }
            else
            {
                Console.WriteLine("  - Заказов нет.");
            }
        }
    }
}

// --- СЛОЙ ПРЕДСТАВЛЕНИЯ ---
public class ConsoleUI
{
    private readonly IClientReader _clientReader;
    private readonly IClientWriter _clientWriter;

    public ConsoleUI(IClientReader clientReader, IClientWriter clientWriter)
    {
        _clientReader = clientReader;
        _clientWriter = clientWriter;
    }

    public void Run(BaseReportGenerator report1, BaseReportGenerator report2)
    {
        Console.WriteLine("--- Система CRM запущена ---");
        _clientWriter.AddClient("Иван Иванов", "ivan@example.com");
        _clientWriter.AddClient("Мария Петрова", "maria@example.com");

        Console.WriteLine("\n--- Генерация простого отчета по клиентам ---");
        report1.Generate();

        Console.WriteLine("\n\n--- Генерация детального отчета по заказам ---");
        report2.Generate();

        _clientWriter.AddClient("Сидоров Вася", "qwerqwer@qwer.swer");
        Console.WriteLine("\n--- Дкмонстрация паттерна Заместитель Proxy ---");
        Console.WriteLine("Первый запрос клиента с ID=1");
        _clientReader.FindClients(new ClientByIdStrategy(1));
        Console.WriteLine("Второй (повторный) запрос клиента с ID=1");
        _clientReader.FindClients(new ClientByIdStrategy(1));

        Console.ReadLine();
    }
}

public class ClientByIdStrategy : IClientSearchStrategy
{
    private readonly int _id;
    public ClientByIdStrategy(int id) { _id = id; }
    public bool IsMatch(Client client) => client.Id.Contains(_id);
}

// --- ТОЧКА ВХОДА (КОРЕНЬ КОМПОЗИЦИИ) ---
public class Program
{
    public static void Main(string[] args)
    {
        var crmService = CrmService.Instance;
        var notifier = new Notifier();
        var ui = new ConsoleUI(crmService, crmService);

        crmService.ClientAdded += notifier.OnClientAdded;

       ReportGeneratorFactory clientReportFactory = new ClientListReportFactory();
       ReportGeneratorFactory orderReportFactory = new ClientOrderReportFactory();

        BaseReportGenerator clientReport = clientReportFactory.CreateGenerator(crmService, crmService);
        BaseReportGenerator ordersReport = clientReportFactory.CreateGenerator(crmService, crmService);

        ui.Run(clientReport, ordersReport);
    }
}

public abstract class ReportGeneratorFactory
{
    public abstract BaseReportGenerator CreateGenerator(IClientReader clientReader, IOrderReader orderReader);
}

public class ClientListReportFactory : ReportGeneratorFactory
{
    public override BaseReportGenerator CreateGenerator(IClientReader clientReader, IOrderReader orderReader)
    {
        return new ClientListReport(clientReader, orderReader);
    }
}

public class ClientOrderReportFactory : ReportGeneratorFactory
{
    public override BaseReportGenerator CreateGenerator(IClientReader clientReader, IOrderReader orderReader)
    {
        return new ClientOrdersReport(clientReader, orderReader);
    }
}

public class ClientRepositoryProxy : IClientRepository
{
    private readonly IClientRepository _readRepository;
    private Dictionary<int, Client> _cache;
    public ClientRepositoryProxy(IClientRepository readRepository)
    {
        _readRepository = readRepository;
        _cache = new Dictionary<int, Client>();
    }
    public void Add(Client entity) => _readRepository.Add(entity);
    public IEnumerable<Client> GetAll() => _readRepository.GetAll();
    public Client GetById(int id) => _readRepository.GetById(id);
    public int GetNextId() => _readRepository.GetNextId();
    public Task SaveAsync() => _readRepository.SaveAsync();
    public Client GetById(int id)
    {
        if (_cache.TryGetValue(id, out var cachedClient))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Proxy] Клиент с ID={id} не неаден в кэше.");
            Console.ResetColor();
            return cachedClient;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Proxy] Клиент с ID={id} не неаден в кэше. Запрос к реальному репозиторию...");
        Console.ResetColor();

        var client = _readRepository.GetById(id);

        if ( client != null )
        {
            _cache[id] = client;
        }

        return client;
    }
    public void Add(Client entity)
    {
        _readRepository.Add(entity);

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[Proxy] Кэш очищен из-за добавления новой записи.");
        Console.ResetColor();
        _cache.Clear();
    }
}