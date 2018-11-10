namespace PhotosiMessageLibrary.DTO
{
    public class Response<T>
    {
        public T Data { get; set; }
        public string ExceptionCode { get; set; }
        public string ExceptionMessage { get; set; }
    }
}