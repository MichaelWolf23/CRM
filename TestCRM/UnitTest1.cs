using Moq;
using Xunit;
using Program;

namespace TestCRM
{
    public class UnitTest1
    {
        [Fact]
        public void RegisterNewClientWithOrder_ShouldCall_AddClientAddOrder()
        {
            // Arrange - Подготовка

            // 1. Создаем mack-объекты для зависимостей
            var mockClientWriter = new Mock<IClientWriter>();
            var mockOrderWriter = new Mock<IOrderWriter>();

            mockClientWriter.Setup(w => w.AddClient(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new Client(1, "Test", "test@test.ru", DateTime.Now));

            var facage = new CrmFacade(mockClientWriter.Object, mockOrderWriter.Object);

            // Ask - действие

            facage.RegisterNewClientWithFirstOrder("Иван", "ivan@test.ru", "Заказ 1", 100);

            // Assert - Проверка

            mockClientWriter.Verify(w => w.AddClient(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            //mockClientWriter.Verify(w => w.AddOrderForClient(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>()), Times.Once());

        }
    }
}
