using System;
using System.Threading.Tasks;

[Serializable()]
public class Element 
{
	private int id { set; get; }
	private int delay { set; get; }
	public Element(int id, int delay)
	{
		this.id = id;
		this.delay = delay;
	}
	
	public void performAction()
  {
		Task.Delay(delay);
  }
}
