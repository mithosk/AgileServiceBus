using AgileSB.Log;
using System.Threading.Tasks;

namespace AgileSB.Interfaces
{
	public interface ILogger
	{
		Task LogAsync(OnRequest data);
		Task LogAsync(OnResponseError data);
		Task LogAsync(OnResponse data);
		Task LogAsync(OnPublish data);
		Task LogAsync(OnConsumeError data);
		Task LogAsync(OnConsumed data);
	}
}