using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using SMTF = Windows.Win32.UI.WindowsAndMessaging.SEND_MESSAGE_TIMEOUT_FLAGS;

namespace WindowCloser;

internal sealed class WindowUtils {
	/// <summary>
	/// Takes a window handle and runs GetWindowThreadProcessId on it.
	/// Necessary because C# won't let you use bare PInvoke.GetWindowThreadProcessId in lambda functions.
	/// </summary>
	/// <param name="handle">The window handle.</param>
	/// <param name="ids">A tuple consisting of (Process ID, Thread ID).</param>
	/// <returns>True if <see cref="ids" /> contains valid values.</returns>
	public static unsafe bool GetWindowThreadProcessId(HWND handle, out (uint processID, uint threadID) ids) {
		uint processID = 0;
		ids.threadID = PInvoke.GetWindowThreadProcessId(handle, &processID);
		ids.processID = processID;
		return ids.threadID != 0;
	}

	public static unsafe bool SendMessageTimeout(HWND handle, uint msg, WPARAM? wParam = null, LPARAM? lParam = null, SMTF flags = SMTF.SMTO_BLOCK | SMTF.SMTO_NOTIMEOUTIFNOTHUNG, uint timeout = 3000) {
		var result = PInvoke.SendMessageTimeout(handle, msg, wParam ?? 0, lParam ?? 0, flags, timeout, null);
		return Convert.ToBoolean(result);
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
	/// <returns>True if the window HWND matches the conditions.</returns>
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

		if (!GetWindowThreadProcessId(windowHandle, out var ids))
			return false;

		try {
			var process = Process.GetProcessById((int)ids.processID);

			if (process.ProcessName != Path.GetFileNameWithoutExtension(processName))
				return false;
		} catch (ArgumentException) {
			// there's no process with this ID
			return false;
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

		_ = PInvoke.EnumWindows(
			(handle, _) => {
				if (CheckWindowHandle(handle, windowInfo, true))
					handles.Add(handle);

				return true;
			},
			0
		);

		return handles;
	}

	public static void MinimizeConsole() {
		if (PInvoke.GetConsoleWindow() is { IsNull: false } handle)
			PInvoke.ShowWindow(handle, SHOW_WINDOW_CMD.SW_MINIMIZE);
	}

	/// <summary>
	/// Tries to close a window, then waits for <see cref="closeTimeout" /> milliseconds.
	/// If the window still exists but is unresponsive, kills the process and waits for up to <see cref="killWait" /> milliseconds for it to exit fully.
	/// </summary>
	/// <param name="windowHandle">The window to close.</param>
	/// <param name="closeTimeout">How long to wait for the window to close, in milliseconds. Set to zero to return immediately.</param>
	/// <param name="killWait">How long to wait for the process to be killed, in milliseconds. Set to zero to return immediately.</param>
	/// <param name="logger">The logger.</param>
	/// <exception cref="CloseWindowException">Something went wrong. Check the exception message for more info.</exception>
	public static void CloseWindowEx(HWND windowHandle, int closeTimeout, int killWait, ILogger<Worker> logger) {
		if (!GetWindowThreadProcessId(windowHandle, out var oldIDs))
			throw new CloseWindowException("Invalid window handle");

		const int ERROR_SUCCESS = (int)WIN32_ERROR.ERROR_SUCCESS;
		Marshal.SetLastSystemError(ERROR_SUCCESS); // prepare for SendMessageTimeout

		logger.LogTrace("Sending WM_SYSCOMMAND/SC_CLOSE…");

		// ask target window to close and wait <timeout> milliseconds
		var success = SendMessageTimeout(windowHandle, PInvoke.WM_SYSCOMMAND, PInvoke.SC_CLOSE, timeout: (uint)closeTimeout);
		if (!success) {
			logger.LogTrace("Failed!");
			var lastError = Marshal.GetLastWin32Error();
			switch (lastError) {
				case ERROR_SUCCESS:
					// generic error
					throw new CloseWindowException("Failed to send WM_SYSCOMMAND/SC_CLOSE to window");
				case (int)WIN32_ERROR.ERROR_TIMEOUT:
					break;
				default:
					var msg = GetErrorMessageForWin32Code(lastError) ?? "Unknown error";
					throw new CloseWindowException($"Failed to send WM_SYSCOMMAND/SC_CLOSE to window: {msg}");
			}
		}

		logger.LogTrace("Success!");

		// check whether the window is still visible
		if (!PInvoke.IsWindowVisible(windowHandle))
			return; // the window isn't visible anymore

		logger.LogTrace("Window is still visible");

		// check whether the window is still there or not and whether it's hanging
		if (!GetWindowThreadProcessId(windowHandle, out var newIDs))
			return; // the window handle is invalid, so it probably doesn't exist anymore

		logger.LogTrace("Window still exists");

		if (oldIDs != newIDs)
			return; // the window handle is valid, but appears to belong to another process/thread now

		logger.LogTrace("Window belongs to same process/thread");

		if (!PInvoke.IsHungAppWindow(windowHandle))
			return; // no idea when this could ever happen

		logger.LogTrace("Window is unresponsive");

		// at this point the window still exists and appears to be unresponsive

		Process process;
		try {
			process = Process.GetProcessById((int)oldIDs.processID);
			logger.LogTrace("Found process {ProcessName}", process.ProcessName);
		} catch (ArgumentException e) {
			throw new CloseWindowException($"Process with ID {oldIDs.processID} doesn't exist", e);
		}

		try {
			logger.LogTrace("Killing process {ProcessName}", process.ProcessName);
			process.Kill();
			process.WaitForExit(killWait);
		} catch (Exception e) {
			throw new CloseWindowException($"Process with ID {oldIDs.processID} couldn't be killed", e);
		}

		logger.LogTrace("Killed process {ProcessName}", process.ProcessName);

		// if we made it this far, the process should be dead
	}
}
