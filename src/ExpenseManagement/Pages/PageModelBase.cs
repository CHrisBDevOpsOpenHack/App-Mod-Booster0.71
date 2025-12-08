using ExpenseManagement.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public abstract class PageModelBase : PageModel
{
    protected void CaptureError(ErrorInfo? error)
    {
        if (error != null)
        {
            ViewData["Error"] = error;
        }
    }

    protected void CaptureError<T>(OperationResult<T> result)
    {
        if (!result.Success)
        {
            CaptureError(result.Error);
        }
    }
}
