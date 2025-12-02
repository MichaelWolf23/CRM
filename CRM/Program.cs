using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

// --- ИНТЕРФЕЙСЫ (КОНТРАКТЫ) ---
public interface IRepository<T> where T : class
{
    IEnumerable<T> GetAll();
    T GetById(int id);
    void Add(T entity);
    Task SaveAsync();
}

public interface IClientRepository : IRepository<Client> { }
public interface IOrderRepository : IRepository<Order> { }


// --- МОДЕЛИ ДАННЫХ ---
public record Client(int Id, string Name, string Email, DateTime CreatedAt);
public record Order(int Id, int ClientId, string Description, decimal Amount, DateOnly DueDate);


// --- СЛОЙ ДОСТУПА К ДАННЫМ (РЕПОЗИТОРИИ) ---
public abstract class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly string _filePath;
    protected List<T> _items;

    protected BaseRepository(string filePath)
    {
        _filePath = filePath;
        _items = new List<T>();
        Load();
    }

    private void Load()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            _items = JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }
    }

    public void Add(T entity) => _items.Add(entity);
    public IEnumerable<T> GetAll() => _items;
    public abstract T GetById(int id);
    public async Task SaveAsync()
    {
        var json = JsonConvert.SerializeObject(_items, Newtonsoft.Json.Formatting.Indented);
        await File.WriteAllTextAsync(_filePath, json);
    }
}

public class ClientRepository : BaseRepository<Client>, IClientRepository
{
    public ClientRepository(string filePath) : base(filePath) { }
    public override Client GetById(int id)
    {
        return _items.FirstOrDefault(c => c.Id == id);
    }
}

public class OrderRepository : BaseRepository<Order>, IOrderRepository
{
    public OrderRepository(string filePath) : base(filePath) { }
    public override Order GetById(int id)
    {
        return _items.FirstOrDefault(o => o.Id == id);
    }
}


// --- СЛОЙ БИЗНЕС-ЛОГИКИ (СЕРВИС) ---
public sealed class CrmService
{
    private readonly IClientRepository _clientRepository;
    private readonly IOrderRepository _orderRepository;

    private static readonly Lazy<CrmService> lazy = new Lazy<CrmService>(() =>
    {
        var clientRepo = new ClientRepository("clients.json");
        var orderRepo = new OrderRepository("orders.json");
        return new CrmService(clientRepo, orderRepo);
    });

    public static CrmService Instance => lazy.Value;

    private CrmService(IClientRepository clientRepository, IOrderRepository orderRepository)
    {
        _clientRepository = clientRepository;
        _orderRepository = orderRepository;
    }

    public Client AddClient(Client client)
    {
        _clientRepository.Add(client);
        _clientRepository.SaveAsync().Wait(); // .Wait() для простоты в консольном приложении
        ClientAdded?.Invoke(client);

        return client;

    }

    public IEnumerable<Client> GetAllClients() => _clientRepository.GetAll();

    public event Action<Client> ClientAdded;
}

public class Nitifier
{
    public void OnClientAdded(Client client)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Уведомления]: Добавлен новый клиент {client.Name} с Email: {client.Email}");
        Console.ResetColor();
    }
}

// --- СЛОЙ ПРЕДСТАВЛЕНИЯ (КОНСОЛЬ) ---
public class Program
{
    public static void Main(string[] args)
    {
        //Console.WriteLine("--- Демонстрация работы сервисного слоя ---");

        //var newClient = new Client(3, "Ольга Иванова", "olga@test.com", DateTime.Now);
        //CrmService.Instance.AddClient(newClient);

        //Console.WriteLine("\nВсе клиенты в системе:");
        //var allClients = CrmService.Instance.GetAllClients();
        //foreach (var client in allClients)
        //{
        //    Console.WriteLine(client);
        //}

        var crmService = CrmService.Instance;
        var nitifier = new Nitifier();

        crmService.ClientAdded += nitifier.OnClientAdded;
        Console.WriteLine("---Система CRM запущена---");
        Console.WriteLine("Добавление нового клиента...");
        var newClient1 = new Client(4, "Ивнов Иван", "top@top-academy.ru", DateTime.Now);
        crmService.AddClient(newClient1);
        Console.WriteLine("Отписываемся от уведомлений");

        crmService.ClientAdded -= nitifier.OnClientAdded;

        var newClient2 = new Client(5, "Семен Иван", "semen@top-academy.ru", DateTime.Now);
        crmService.AddClient(newClient2);

        Console.ReadLine();
    }
}


