using System.Text;
namespace UCIS {
	public class UTF8NoPreamble : UTF8Encoding {
		public UTF8NoPreamble() : base(false) { }
	}
}
