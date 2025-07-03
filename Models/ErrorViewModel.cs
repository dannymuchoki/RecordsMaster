namespace RecordsMaster.Models;

// This class is used to represent the error view model in the application.
// It contains a property for the request ID and a boolean to determine if the request ID should be shown.
// The request ID is typically used for tracking errors in logs or for debugging purposes.      
// The ShowRequestId property returns true if the RequestId is not null or empty, indicating that there is a request ID to display.
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
