using Microsoft.AspNetCore.Mvc;
using ForexExchange.Helpers;

namespace ForexExchange.Controllers
{
    /// <summary>
    /// Test controller to demonstrate consistent date formatting throughout the application
    /// </summary>
    public class DateFormatTestController : Controller
    {
        /// <summary>
        /// Test action to display various date formats consistently
        /// </summary>
        public IActionResult Index()
        {
            var testData = new DateFormatTestViewModel
            {
                CurrentDate = DateTime.Now,
                SampleDate = new DateTime(2024, 12, 25, 14, 30, 45),
                NullableDate = new DateTime(2023, 6, 15),
                NullDate = null,
                DateDisplayFormat = DateTimeHelper.DateDisplayFormat,
                DateTimeDisplayFormat = DateTimeHelper.DateTimeDisplayFormat,
                CurrentDateDisplay = DateTimeHelper.CurrentDateDisplay,
                CurrentDateTimeDisplay = DateTimeHelper.CurrentDateTimeDisplay,
                Examples = new List<DateExample>
                {
                    new DateExample 
                    { 
                        Description = "Current Date (ToDisplayDate)", 
                        Value = DateTime.Now.ToDisplayDate() 
                    },
                    new DateExample 
                    { 
                        Description = "Current DateTime (ToDisplayDateTime)", 
                        Value = DateTime.Now.ToDisplayDateTime() 
                    },
                    new DateExample 
                    { 
                        Description = "Sample Date for HTML Input (ToInputDate)", 
                        Value = DateTime.Now.ToInputDate() 
                    },
                    new DateExample 
                    { 
                        Description = "Full DateTime Display (ToFullDisplayDateTime)", 
                        Value = DateTime.Now.ToFullDisplayDateTime() 
                    },
                    new DateExample 
                    { 
                        Description = "Using ToPersianDateTextify (now standard format)", 
                        Value = DateTime.Now.ToPersianDateTextify() 
                    },
                    new DateExample 
                    { 
                        Description = "Nullable Date Example", 
                        Value = ((DateTime?)new DateTime(2024, 3, 21)).ToPersianDateTextify() 
                    },
                    new DateExample 
                    { 
                        Description = "Null Date Example", 
                        Value = ((DateTime?)null).ToPersianDateTextify() 
                    }
                }
            };

            return View(testData);
        }

        /// <summary>
        /// Test date parsing functionality
        /// </summary>
        [HttpPost]
        public IActionResult TestParsing(string dateString)
        {
            var result = new DateParsingTestResult
            {
                InputString = dateString
            };

            try
            {
                if (DateTimeHelper.TryParseDisplayDate(dateString, out DateTime parsedDate))
                {
                    result.Success = true;
                    result.ParsedDate = parsedDate;
                    result.FormattedBack = parsedDate.ToDisplayDate();
                    result.Message = "Date parsed successfully";
                }
                else
                {
                    result.Success = false;
                    result.Message = "Invalid date format. Expected format: yyyy-MM-dd";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error parsing date: {ex.Message}";
            }

            return Json(result);
        }
    }

    /// <summary>
    /// View model for date format testing
    /// </summary>
    public class DateFormatTestViewModel
    {
        public DateTime CurrentDate { get; set; }
        public DateTime SampleDate { get; set; }
        public DateTime? NullableDate { get; set; }
        public DateTime? NullDate { get; set; }
        public string DateDisplayFormat { get; set; } = "";
        public string DateTimeDisplayFormat { get; set; } = "";
        public string CurrentDateDisplay { get; set; } = "";
        public string CurrentDateTimeDisplay { get; set; } = "";
        public List<DateExample> Examples { get; set; } = new();
    }

    /// <summary>
    /// Example of date formatting
    /// </summary>
    public class DateExample
    {
        public string Description { get; set; } = "";
        public string Value { get; set; } = "";
    }

    /// <summary>
    /// Result of date parsing test
    /// </summary>
    public class DateParsingTestResult
    {
        public string InputString { get; set; } = "";
        public bool Success { get; set; }
        public DateTime? ParsedDate { get; set; }
        public string FormattedBack { get; set; } = "";
        public string Message { get; set; } = "";
    }
}