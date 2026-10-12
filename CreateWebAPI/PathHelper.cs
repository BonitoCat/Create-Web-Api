namespace Create_Web_API;

public static class PathHelper
{
    public static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        string trimmed = path.Trim();

        if (trimmed == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (trimmed.StartsWith("~/") || trimmed.StartsWith("~\\"))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string remainder = trimmed[2..];
            return Path.GetFullPath(Path.Combine(home, remainder));
        }

        return Path.GetFullPath(trimmed);
    }
}
