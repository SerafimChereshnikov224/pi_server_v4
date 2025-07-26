namespace PiServer.version_2.models
{
    // Запрос на создание процесса
    public class CreateProcessRequest
    {
        public string Definition { get; set; }  // Строка с процессом, например: "(νx)(x![z].0 | x?(y).y![x].0) | z?(v).v![v].0"
    }

    // Ответ с состоянием процесса
    public class ProcessStateResponse
    {
        public string Id { get; set; }          // ID процесса
        public string CurrentState { get; set; } // Текущее состояние в виде строки
        public string LastAction { get; set; }  // Последнее выполненное действие
        public bool IsCompleted { get; set; }   // Завершен ли процесс
    }
}
