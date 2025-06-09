namespace WindowCloser {
	public sealed class Settings {
		public double Interval { get; set; } = 1;
		public List<WindowInfo> Windows { get; set; } = [];
	}

	public sealed class WindowInfo {
		public string? Title { get; set; }
		public string? Class { get; set; }
		public string? Process { get; set; }
		public bool Multiple { get; set; }
		public bool CheckVisible { get; set; } = true;

		public bool IsValid => !(this.Title == null && this.Class == null);

		public string FancyName => this.Title ?? this.Class ?? this.Process ?? "N/A";

		public override string ToString() {
			if (!this.IsValid)
				return "Invalid Window";

			var str = "";
			if (this.Title is not null && this.Class is not null)
				str += $"{this.Title}/{this.Class}";
			else if (this.Title is not null)
				str += $"{this.Title}";
			else if (this.Class is not null)
				str += $"{this.Class}";

			if (this.Process is not null)
				str += $" ({this.Process})";

			if (this.Multiple)
				str += "+";

			if (this.CheckVisible)
				str += "👁️";

			return str;
		}
	}
}
