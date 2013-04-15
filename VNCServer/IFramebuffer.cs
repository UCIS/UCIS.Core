using System;
using System.Drawing;

namespace UCIS.VNCServer {
	/// <summary>
	/// A generic graphic framebuffer interface that provides functions to draw to and copy from the framebuffer
	/// </summary>
	public interface IFramebuffer {
		/// <summary>
		/// The width of the framebuffer in pixels
		/// </summary>
		int Width { get; }
		/// <summary>
		/// The height of the framebuffer in pixels
		/// </summary>
		int Height { get; }
		/// <summary>
		/// Clear the display area
		/// </summary>
		void Clear();
		/// <summary>
		/// Draw part of an Image to the screen
		/// </summary>
		/// <remarks>Best performance is provided with Bitmap images.</remarks>
		/// <param name="image">The Image object to copy from</param>
		/// <param name="srcrect">The area in the image to copy</param>
		/// <param name="dest">The position on screen to copy to</param>
		void DrawImage(Image image, Rectangle srcrect, Point dest);
		/// <summary>
		/// Draw part of a 32 bits per pixel bitmap to the screen
		/// </summary>
		/// <param name="bitmap">The array that contains the Bitmap data (one pixel per entry)</param>
		/// <param name="bmwidth">The width of the Bitmap data</param>
		/// <param name="srcrect">The area in the bitmap to copy</param>
		/// <param name="dest">The position on screen to copy to</param>
		void DrawPixels(int[] bitmap, int bmwidth, Rectangle srcrect, Point dest);
		/// <summary>
		/// Draw part of a 32 bits per pixel bitmap to the screen
		/// </summary>
		/// <param name="bitmap">The pointer to the start of the Bitmap data</param>
		/// <param name="bmwidth">The width of the Bitmap data</param>
		/// <param name="srcrect">The area in the bitmap to copy</param>
		/// <param name="dest">The position on screen to copy to</param>
		void DrawPixels(IntPtr bitmap, int bmwidth, Rectangle srcrect, Point dest);

		/// <summary>
		/// Copy an area on the display
		/// </summary>
		/// <param name="srcrect">The area to copy from</param>
		/// <param name="dest">Where to copy the area to</param>
		void CopyRectangle(Rectangle srcrect, Point dest);

		/// <summary>
		/// Copy an area from this framebuffer to another framebuffer
		/// </summary>
		/// <remarks>Not all framebuffer implementations may support this operation, notably because some framebuffers can only be written to</remarks>
		/// <param name="srcrect">The area to copy</param>
		/// <param name="destbuffer">The framebuffer to copy to</param>
		/// <param name="destposition">The position in the destination framebuffer to copy to</param>
		void CopyRectangleTo(Rectangle srcrect, IFramebuffer destbuffer, Point destposition);
	}
}
