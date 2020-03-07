namespace CoreDapperRepository.Web.Models.Customers
{
    public class SearchCustomerModel : SearchPagedModel
    {
        public string Username { get; set; }

        public string Email { get; set; }
    }
}
