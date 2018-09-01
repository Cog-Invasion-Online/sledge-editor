using System.Drawing;
using System.Threading.Tasks;
using Sledge.BspEditor.Environment;

namespace Sledge.BspEditor.Grid
{
    public interface IGridFactory
    {
        /// <summary>
        /// The name of the grid
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// A short explanation of the grid
        /// </summary>
        string Details { get; set; }

        Image Icon { get; }


        /// <summary>
        /// Create a grid for the provided environment
        /// </summary>
        /// <param name="environment"></param>
        /// <returns></returns>
        Task<IGrid> Create(IEnvironment environment);

        /// <summary>
        /// Test if a grid is an instance of this factory class
        /// </summary>
        /// <param name="grid"></param>
        /// <returns></returns>
        bool IsInstance(IGrid grid);
    }
}