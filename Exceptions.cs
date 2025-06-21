namespace WindowCloser;

[Serializable]
public class CloseWindowException: Exception {
	public CloseWindowException() { }

	public CloseWindowException(string message) : base(message) { }

	public CloseWindowException(string message, Exception innerException) : base(message, innerException) { }
}