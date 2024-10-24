namespace VtecInventory.Models
{
    public class ApiResponse<T>
    {
        public string ResponseCode { get; set; } = "";
        public string ResponseText { get; set; } = "";
        public T ResponseObj { get; set; }
    }
}
