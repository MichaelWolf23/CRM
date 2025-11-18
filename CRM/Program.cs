using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class BaseRepository<T>
{
    protected List<T> _items;
    protected readonly string _filePath;
    protected int _nextId = 1;
    protected BaseRepository(string filePath)
    {
        _filePath = filePath;
        _items = new List<T>();
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        string json = File.ReadAllText(_filePath);
        _items = JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
    }

    public virtual async Task SaveAsync()
    {
        string json = JsonConvert.SerializeObject(_items, Newtonsoft.Json.Formatting.Indented);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public List<T> GetAll() => _items;
}

public class ClientRepository : BaseRepository<Client>
{
    public ClientRepository(string filePath) : base(filePath)
    {
        if (_items.Any())
        {
            _nextId = _items.Cast<Client>().Max(c => c.Id) + 1;
        }
    }

    public Client Add(string name, string email)
    {
        var client = new Client(_nextId++, name, email, DateTime.Now);
        _items.Add(client);
        return client;
    }

    public Client? GetById(int id)
    {
        return _items.Cast<Client>().FirstOrDefault(c => c.Id == id);
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- Тестирование моделей данных ---");

        string clientFile = "clients.json";
        var clientRepo = new ClientRepository(clientFile);

        Console.WriteLine("--- Начальный список клиентов ---");
        PrintClient(clientRepo.GetAll());

        Console.WriteLine("--- Добавляем нового пользователя ---");
        clientRepo.Add("Новый клиент", "new@top-academy.ru");
        PrintClient(clientRepo.GetAll());
        
        await clientRepo.SaveAsync();
        Console.WriteLine("Данные сохранены в файл");
    }
    public static void PrintClient(List<Client> clients)
    {
        if (!clients.Any())
        {
            Console.WriteLine($"Список клиентов пуст!");
            return;
        }
        foreach(var client in clients)
        {
            Console.WriteLine(client);
        }
    }

}

public record Client(int Id, string Name, string Email, DateTime CreateAt);
public record Order(int Id, int ClientId, string Description, decimal Amount, DateOnly DueDare);
