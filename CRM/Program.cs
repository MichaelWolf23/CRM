public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("--- Тестирование моделей данных ---");

        //создание клиента
        var client1 = new Client(1, "Михаил", "Почта.ru", DateTime.Now);
        Console.WriteLine("Создание клиента: ");
        Console.WriteLine(client1);

        //заказ для клиента
        var order1 = new Order(101, client1.Id, "Название заказа", 100.00m, DateOnly.FromDateTime(DateTime.Now.AddDays(30)));
        Console.WriteLine("Заказ создан: ");
        Console.WriteLine(order1);

        //сравнение значений
        var client1_copy = new Client(1, "Михаил", "Почта.ru", DateTime.Now);
        Console.WriteLine($"Равны ли client1 и client1_copy? --> {client1 == client1_copy}");

    }
}

public record Client(int Id, string Name, string Email, DateTime CreateAt);
public record Order(int Id, int ClientId, string Description, decimal Amount, DateOnly DueDare);
