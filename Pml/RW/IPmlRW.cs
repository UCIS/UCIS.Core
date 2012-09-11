namespace UCIS.Pml {
	public interface IPmlReader {
		PmlElement ReadMessage();
	}
	public interface IPmlWriter {
		void WriteMessage(PmlElement Message);
	}
	public interface IPmlRW : IPmlReader, IPmlWriter {
	}
}