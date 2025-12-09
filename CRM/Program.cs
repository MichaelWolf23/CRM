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

public interface IClientSearchStrategy
{
    bool IsMatch(Client client);
}

// --- МОДЕЛИ ДАННЫХ ---
public record Client(int Id, string Name, string Email, DateTime CreatedAt) : IEntity;
public record Order(int Id, int ClientId, string Description, decimal Amount, DateOnly DueDate): IEntity;


// --- СЛОЙ ДОСТУПА К ДАННЫМ (РЕПОЗИТОРИИ) ---

public class SearchClientByNameStrategy : IClientSearchStrategy
{
    private readonly string _name;
    public SearchClientByNameStrategy(string name)
    {
        _name = name.ToLower();
    }

    public bool IsMatch(Client client)
    {
        return client.Name.ToLower().Contains(_name);
    }
}
public class SearchClientByEmailStrategy : IClientSearchStrategy
{
    private readonly string _emailDomain;
    public SearchClientByEmailStrategy(string emailDomain)
    {
        _emailDomain = emailDomain.ToLower();
    }

    public bool IsMatch(Client client)
    {
        return client.Email.ToLower().Contains(_emailDomain);
    }
}
public abstract class BaseRepository<T> : IRepository<T> where T : class, IEntity
{
    protected List<T> _items;
    private readonly IStorage<T> _storage;


    protected BaseRepository(IStorage<T> storage)
    {
        _storage = storage;
        _items = _storage.Load();
    }
    public async Task SaveAsync()
    {
        await _storage.SaveAsync(_items);
    }
    public int GetNextId()
    {
        return _items.Any() ? _items.Max(e => e.Id) + 1 : 1;
    }
    public void Add(T entity) => _items.Add(entity);
    public IEnumerable<T> GetAll() => _items;
    public abstract T GetById(int id);
}

public interface IEntity
{
    int Id { get; }
}



public interface IStorage<T>
{
    Task SaveAsync(List<T> item);
    List<T> Load();
}

public class JsonFileStorage<T> : IStorage<T>
{
    private readonly string _filePath;
    public JsonFileStorage(string filePath)
    {
        _filePath = filePath;
    }
    public List<T> Load()
    {
        if (!File.Exists(_filePath))
        {
            return new List<T>();
        }
        var json = File.ReadAllText(_filePath);
        return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
    }
    public async Task SaveAsync(List<T> item)
    {
        var ison = JsonConvert.SerializeObject(item, Newtonsoft.Json.Formatting.Indented);
        await File.WriteAllTextAsync(_filePath, ison);
    }
}

public class ClientRepository : BaseRepository<Client>, IClientRepository
{
    public ClientRepository(IStorage<Client> storage) : base(storage) { }
    public override Client GetById(int id)
    {
        return _items.FirstOrDefault(c => c.Id == id);
    }
}

public class OrderRepository : BaseRepository<Order>, IOrderRepository
{
    public OrderRepository(IStorage<Order> storage) : base(storage) { }
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

    public Client AddClient(Client client)
    {
        _clientRepository.Add(client);
        _clientRepository.SaveAsync().Wait(); // .Wait() для простоты в консольном приложении
        ClientAdded?.Invoke(client);

        return client;

    }

    public IEnumerable<Client> GetAllClients() => _clientRepository.GetAll();

    public event Action<Client> ClientAdded;
    public IEnumerable<Client> FindClients(IClientSearchStrategy strategy)
    {
        return _clientRepository.GetAll().Where(client => strategy.IsMatch(client));
    }
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

public class ConsoleUI
{
    private readonly CrmService _crmServer;

    public ConsoleUI(CrmService crmServer)
    {
        _crmServer = crmServer;
    }

    public void Ran()
    {
        _crmServer.AddClient(new Client(6, "Иванов Иван", "test@top.com", DateTime.Now));
        _crmServer.AddClient(new Client(7, "Семен Иван", "test@top.com", DateTime.Now));
        _crmServer.AddClient(new Client(8, "Костин Влад", "Kostin@top.com", DateTime.Now));

        var nameStrategy = new SearchClientByNameStrategy("Иван");
        var foundByName = _crmServer.FindClients(nameStrategy);
        Console.WriteLine("Найдены клиенты по имени иван:");
        foreach (var client in foundByName)
        {
            Console.WriteLine(client);
        }

        var emailStrategy = new SearchClientByEmailStrategy("test@top.com");
        var foundByEmail = _crmServer.FindClients(emailStrategy);
        Console.WriteLine("Найдены клиенты с почтой test@top.com:");
        foreach (var client in foundByEmail)
        {
            Console.WriteLine(client);
        }

    }
}

// --- СЛОЙ ПРЕДСТАВЛЕНИЯ (КОНСОЛЬ) ---
public class Program
{
    public static void Main(string[] args)
    {
        var crmService = CrmService.Instance;
        var nitifier = new Nitifier();
        var ui = new ConsoleUI(crmService);

        crmService.ClientAdded += nitifier.OnClientAdded;

        ui.Ran();
    }
}


