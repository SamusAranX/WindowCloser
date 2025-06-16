using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace WindowCloser;

internal sealed class WindowUtils {
	/// <summary>
	/// Takes a window handle and runs GetWindowThreadProcessId on it.
	/// Necessary because C# won't let you use bare PInvoke.GetWindowThreadProcessId in lambda functions.
	/// </summary>
	/// <param name="handle">The window handle.</param>
	/// <returns>A tuple consisting of (Process ID, Thread ID).</returns>
	public static unsafe (uint, uint) GetWindowThreadProcessId(HWND handle) {
		uint processID = 0;
		var threadID = PInvoke.GetWindowThreadProcessId(handle, &processID);
		return (processID, threadID);
	}

	public static string? GetErrorMessageForWin32Code(int w32Error) {
		var hr = PInvoke.HRESULT_FROM_WIN32((WIN32_ERROR)w32Error);
		var ex = Marshal.GetExceptionForHR(hr);
		return ex?.Message;
	}

	/// <summary>
	/// Checks a window HWND against the conditions set out in a WindowInfo object.
	/// </summary>
	/// <param name="windowHandle">The window HWND to check.</param>
	/// <param name="windowInfo">The WindowInfo to check against.</param>
	/// <param name="enumWindows">Whether this function is being called from within an EnumWindows lambda. Enables manual title and class checks.</param>
	/// <returns>True if the window HWND matches the conditions. False otherwise.</returns>
	public static bool CheckWindowHandle(HWND windowHandle, WindowInfo windowInfo, bool enumWindows = false) {
		if (windowHandle.IsNull)
			return false;

		if (windowInfo.CheckVisible && !PInvoke.IsWindowVisible(windowHandle))
			return false;

		if (enumWindows) {
			if (windowInfo.Title is string windowTitle) {
				Span<char> windowTitleSpan = stackalloc char[256];
				PInvoke.GetWindowText(windowHandle, windowTitleSpan);
				if (windowTitle != windowTitleSpan.ToString().TrimEnd('\0'))
					return false;
			}

			if (windowInfo.Class is string windowClass) {
				Span<char> classNameSpan = stackalloc char[256];
				PInvoke.GetClassName(windowHandle, classNameSpan);
				if (windowClass != classNameSpan.ToString().TrimEnd('\0'))
					return false;
			}
		}

		if (windowInfo.Process is not string processName)
			return true;

		var (processID, _) = GetWindowThreadProcessId(windowHandle);

		try {
			var process = Process.GetProcessById((int)processID);

			if (process.ProcessName != Path.GetFileNameWithoutExtension(processName))
				return false;
		} catch (ArgumentException ) {
			Console.WriteLine();
			throw;
		}

		return true;
	}

	public static HWND? FindOneWindow(WindowInfo windowInfo) {
		if (!windowInfo.IsValid)
			return null;

		var windowHandle = PInvoke.FindWindow(windowInfo.Class, windowInfo.Title);
		if (CheckWindowHandle(windowHandle, windowInfo))
			return windowHandle;

		return null;
	}

	public static List<HWND> FindManyWindows(WindowInfo windowInfo) {
		var handles = new List<HWND>();

		_ = PInvoke.EnumWindows((handle, _) => {
				if (CheckWindowHandle(handle, windowInfo, true))
					handles.Add(handle);

				return true;
			},
			new LPARAM(0));

		return handles;
	}

	public static bool CloseWindow(HWND windowHandle) {
		return PInvoke.PostMessage(windowHandle, PInvoke.WM_CLOSE, 0, 0);
	}
}