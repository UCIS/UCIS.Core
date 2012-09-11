namespace UCIS {
	public class UTF8NoPreamble : System.Text.UTF8Encoding {
		public override byte[] GetPreamble() {
			return new byte[] { };
		}
	}
}
