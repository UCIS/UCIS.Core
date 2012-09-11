using System;
using System.Collections.Generic;
using System.Text;

namespace UCIS.Pml {
	public class PmlBuilder {
		private IPmlWriter pWriter;
		private Stack<PmlElement> pStack = new Stack<PmlElement>();

		public PmlBuilder(IPmlWriter Writer) {
			pWriter = Writer;
		}
		public PmlBuilder() {
			pWriter = null;
		}

		public IPmlWriter BaseWriter {
			get { return pWriter; }
			set { pWriter = value; }
		}

		private PmlElement AddChildElement(PmlElement Element, bool AddToStack) {
			return AddChildElement(Element, AddToStack, null);
		}
		private PmlElement AddChildElement(PmlElement Element, bool AddToStack, string ChildName) {
			PmlElement Parent;
			if (pStack.Count > 0) {
				Parent = pStack.Peek();
				if (Parent is PmlDictionary) {
					if (ChildName == null) throw new ArgumentNullException("ChildName", "Dictionary items need a Name");
					((PmlDictionary)Parent).Add(ChildName, Element);
				} else if (Parent is PmlCollection) {
					if (ChildName != null) throw new ArgumentOutOfRangeException("ChildName", "Can not add named element to a Collection");
					((PmlCollection)Parent).Add(Element);
				} else {
					throw new InvalidOperationException("Invalid Element type in stack: " + Parent.Type.ToString());
				}
			} else {
				if (ChildName != null) {
					throw new ArgumentOutOfRangeException("ChildName", "Can not create named element without container (Dictionary)");
				} else if (!AddToStack) {
					pWriter.WriteMessage(Element);
				}
			}
			if (AddToStack) pStack.Push(Element);
			return Element;
		}

		public PmlElement EndElement() {
			if (pStack.Count > 0) {
				PmlElement Element = pStack.Pop();
				if (pStack.Count == 0) {
					if (pWriter != null) pWriter.WriteMessage(Element);
				}
				return Element;
			} else {
				return null;
			}
		}
		public PmlElement GetMessage() {
			if (pStack.Count == 1) {
				PmlElement Element = pStack.Pop();
				return Element;
			} else if (pStack.Count == 0) {
				throw new InvalidOperationException("No stacked element. The top most element should not be ended. All elements, except Dictionary and Collection, are sent automatically.");
			} else {
				throw new InvalidOperationException("All elements, except for the top most element, should be ended.");
			}
		}

		public PmlElement SendMessage() {
			PmlElement Element;
			if (pWriter == null) throw new NullReferenceException("Writer is not set");
			Element = GetMessage();
			pWriter.WriteMessage(Element);
			return Element;
		}

		public PmlDictionary Dictionary() {
			return (PmlDictionary)AddChildElement(new PmlDictionary(), true);
		}
		public PmlCollection Collection() {
			return (PmlCollection)AddChildElement(new PmlCollection(), true);
		}
		public PmlString String(string Value) {
			return (PmlString)AddChildElement(new PmlString(Value), false);
		}
		public PmlBinary Binary(byte[] Value) {
			return (PmlBinary)AddChildElement(new PmlBinary(Value), false);
		}
		public PmlInteger Integer(UInt64 Value) {
			return (PmlInteger)AddChildElement(new PmlInteger(Value), false);
		}
		public PmlInteger Integer(Int64 Value) {
			return (PmlInteger)AddChildElement(new PmlInteger(Value), false);
		}
		public PmlNull Null() {
			return (PmlNull)AddChildElement(new PmlNull(), false);
		}
		public PmlElement Element(PmlElement Child) {
			return AddChildElement(Child, false);
		}

		public PmlDictionary Dictionary(string Name) {
			return (PmlDictionary)AddChildElement(new PmlDictionary(), true, Name);
		}
		public PmlCollection Collection(string Name) {
			return (PmlCollection)AddChildElement(new PmlCollection(), true, Name);
		}
		public PmlString String(string Name, string Value) {
			return (PmlString)AddChildElement(new PmlString(Value), false, Name);
		}
		public PmlBinary Binary(string Name, byte[] Value) {
			return (PmlBinary)AddChildElement(new PmlBinary(Value), false, Name);
		}
		public PmlInteger Integer(string Name, UInt64 Value) {
			return (PmlInteger)AddChildElement(new PmlInteger(Value), false, Name);
		}
		public PmlInteger Integer(string Name, Int64 Value) {
			return (PmlInteger)AddChildElement(new PmlInteger(Value), false, Name);
		}
		public PmlNull Null(string Name) {
			return (PmlNull)AddChildElement(new PmlNull(), false, Name);
		}
		public PmlElement Element(string Name, PmlElement Child) {
			return AddChildElement(Child, false, Name);
		}
	}
}