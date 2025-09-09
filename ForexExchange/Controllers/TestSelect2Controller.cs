using Microsoft.AspNetCore.Mvc;

namespace ForexExchange.Controllers
{
    public class TestSelect2Controller : Controller
    {
        public IActionResult Index()
        {
            // Sample data for dropdown testing
            ViewBag.SampleOptions = new List<dynamic>
            {
                new { Value = "1", Text = "گزینه اول" },
                new { Value = "2", Text = "گزینه دوم" },
                new { Value = "3", Text = "گزینه سوم" },
                new { Value = "4", Text = "گزینه چهارم" },
                new { Value = "5", Text = "گزینه پنجم" },
                new { Value = "6", Text = "گزینه ششم" },
                new { Value = "7", Text = "گزینه هفتم" },
                new { Value = "8", Text = "گزینه هشتم" },
                new { Value = "9", Text = "گزینه نهم" },
                new { Value = "10", Text = "گزینه دهم" }
            };

            return View();
        }
    }
}
