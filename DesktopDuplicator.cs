using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.IO;

using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Rectangle = SharpDX.Rectangle;
using DrawRect = System.Drawing.Rectangle;

namespace PoorMansAmbilight {
	delegate void OnFrameFunc(DesktopDuplicator This, int Width, int Height);

	[Serializable]
	public class DesktopDuplicationException : Exception {
		public DesktopDuplicationException() {
		}

		public DesktopDuplicationException(string message) : base(message) {
		}

		public DesktopDuplicationException(string message, Exception inner) : base(message, inner) {
		}

		protected DesktopDuplicationException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) {
		}
	}

	public struct MovedRegion {
		/// <summary>
		/// Gets the location from where the operating system copied the image region.
		/// </summary>
		public System.Drawing.Point Source {
			get; internal set;
		}

		/// <summary>
		/// Gets the target region to where the operating system moved the image region.
		/// </summary>
		public DrawRect Destination {
			get; internal set;
		}
	}

	internal class PointerInfo {
		public byte[] PtrShapeBuffer;
		public OutputDuplicatePointerShapeInformation ShapeInfo;
		public SharpDX.Point Position;
		public bool Visible;
		public int BufferSize;
		public int WhoUpdatedPositionLast;
		public long LastTimeStamp;
	}

	public class DesktopFrame {
		public Bitmap DesktopImage {
			get; internal set;
		}

		public MovedRegion[] MovedRegions {
			get; internal set;
		}

		public System.Drawing.Rectangle[] UpdatedRegions {
			get; internal set;
		}

		public int AccumulatedFrames {
			get; internal set;
		}

		public System.Drawing.Point CursorLocation {
			get; internal set;
		}

		public bool CursorVisible {
			get; internal set;
		}

		public bool ProtectedContentMaskedOut {
			get; internal set;
		}

		public bool RectanglesCoalesced {
			get; internal set;
		}
	}

	unsafe class DesktopDuplicator {
		private Device mDevice;
		private Texture2DDescription mTextureDesc;
		private OutputDescription mOutputDesc;
		private OutputDuplication mDeskDupl;

		private Texture2D desktopImageTexture = null;
		private OutputDuplicateFrameInformation frameInfo = new OutputDuplicateFrameInformation();
		private int mWhichOutputDevice = -1;

		private Bitmap finalImage1, finalImage2;
		private bool isFinalImage1 = false;

		private Bitmap FinalImage {
			get {
				return isFinalImage1 ? finalImage1 : finalImage2;
			}

			set {
				if (isFinalImage1) {
					finalImage2 = value;

					if (finalImage1 != null)
						finalImage1.Dispose();
				} else {
					finalImage1 = value;
					if (finalImage2 != null)
						finalImage2.Dispose();
				}
				isFinalImage1 = !isFinalImage1;
			}
		}

		public event OnFrameFunc OnFrame;

		public DesktopDuplicator(int whichMonitor)
			: this(0, whichMonitor) { }

		public DesktopDuplicator(int whichGraphicsCardAdapter, int whichOutputDevice) {
			this.mWhichOutputDevice = whichOutputDevice;
			Adapter1 adapter = null;

			try {
				adapter = new Factory1().GetAdapter1(whichGraphicsCardAdapter);
			} catch (SharpDXException) {
				throw new DesktopDuplicationException("Could not find the specified graphics card adapter.");
			}

			this.mDevice = new Device(adapter);
			Output output = null;

			try {
				output = adapter.GetOutput(whichOutputDevice);
			} catch (SharpDXException) {
				throw new DesktopDuplicationException("Could not find the specified output device.");
			}

			var output1 = output.QueryInterface<Output1>();
			this.mOutputDesc = output.Description;

			this.mTextureDesc = new Texture2DDescription() {
				CpuAccessFlags = CpuAccessFlags.Read,
				BindFlags = BindFlags.None,
				Format = Format.B8G8R8A8_UNorm,

				Width = this.mOutputDesc.DesktopBounds.GetWidth(),
				Height = this.mOutputDesc.DesktopBounds.GetHeight(),



				OptionFlags = ResourceOptionFlags.None,
				MipLevels = 1,
				ArraySize = 1,
				SampleDescription = { Count = 1, Quality = 0 },
				Usage = ResourceUsage.Staging
			};

			try {
				this.mDeskDupl = output1.DuplicateOutput(mDevice);
			} catch (SharpDXException ex) {
				if (ex.ResultCode.Code == SharpDX.DXGI.ResultCode.NotCurrentlyAvailable.Result.Code) {
					throw new DesktopDuplicationException("There is already the maximum number of applications using the Desktop Duplication API running, please close one of the applications and try again.");
				}
			}
		}

		DataBox MapSrc;
		byte* Framebuffer;

		public DesktopFrame GetLatestFrame() {
			var frame = new DesktopFrame();

			bool retrievalTimedOut = RetrieveFrame();
			if (retrievalTimedOut)
				return null;

			RetrieveFrameMetadata(frame);
			RetrieveCursorMetadata(frame);

			// Lock
			MapSrc = mDevice.ImmediateContext.MapSubresource(desktopImageTexture, 0, MapMode.Read, MapFlags.None);
			Framebuffer = (byte*)MapSrc.DataPointer;

			// Process
			OnFrame?.Invoke(this, mOutputDesc.DesktopBounds.GetWidth(), mOutputDesc.DesktopBounds.GetHeight());

			// Unlock
			mDevice.ImmediateContext.UnmapSubresource(desktopImageTexture, 0);

			ReleaseFrame();
			return frame;
		}

		public PixelColor GetPixel(int X, int Y) {
			return *(PixelColor*)(Framebuffer + (Y * MapSrc.RowPitch) + X * sizeof(PixelColor));
		}

		private bool RetrieveFrame() {
			if (desktopImageTexture == null)
				desktopImageTexture = new Texture2D(mDevice, mTextureDesc);

			SharpDX.DXGI.Resource desktopResource = null;
			frameInfo = new OutputDuplicateFrameInformation();

			try {
				mDeskDupl.AcquireNextFrame(500, out frameInfo, out desktopResource);
			} catch (SharpDXException ex) {
				if (ex.ResultCode.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code) {
					return true;
				}
				if (ex.ResultCode.Failure) {
					throw new DesktopDuplicationException("Failed to acquire next frame.");
				}
			}

			using (var tempTexture = desktopResource.QueryInterface<Texture2D>())
				mDevice.ImmediateContext.CopyResource(tempTexture, desktopImageTexture);

			desktopResource.Dispose();
			return false;
		}

		private void RetrieveFrameMetadata(DesktopFrame frame) {
			if (frameInfo.TotalMetadataBufferSize > 0) {
				// Get moved regions
				int movedRegionsLength = 0;
				OutputDuplicateMoveRectangle[] movedRectangles = new OutputDuplicateMoveRectangle[frameInfo.TotalMetadataBufferSize];
				mDeskDupl.GetFrameMoveRects(movedRectangles.Length, movedRectangles, out movedRegionsLength);
				frame.MovedRegions = new MovedRegion[movedRegionsLength / Marshal.SizeOf(typeof(OutputDuplicateMoveRectangle))];

				for (int i = 0; i < frame.MovedRegions.Length; i++) {
					frame.MovedRegions[i] = new MovedRegion() {
						Source = new System.Drawing.Point(movedRectangles[i].SourcePoint.X, movedRectangles[i].SourcePoint.Y),
						Destination = new DrawRect(movedRectangles[i].DestinationRect.GetX(), movedRectangles[i].DestinationRect.GetY(), movedRectangles[i].DestinationRect.GetWidth(), movedRectangles[i].DestinationRect.GetHeight())
					};
				}

				// Get dirty regions
				int dirtyRegionsLength = 0;
				RawRectangle[] dirtyRectangles = new RawRectangle[frameInfo.TotalMetadataBufferSize];

				mDeskDupl.GetFrameDirtyRects(dirtyRectangles.Length, dirtyRectangles, out dirtyRegionsLength);

				frame.UpdatedRegions = new DrawRect[dirtyRegionsLength / Marshal.SizeOf(typeof(Rectangle))];

				for (int i = 0; i < frame.UpdatedRegions.Length; i++) {
					frame.UpdatedRegions[i] = new DrawRect(dirtyRectangles[i].GetX(), dirtyRectangles[i].GetY(), dirtyRectangles[i].GetWidth(), dirtyRectangles[i].GetHeight());
				}
			} else {
				frame.MovedRegions = new MovedRegion[0];
				frame.UpdatedRegions = new DrawRect[0];
			}
		}

		private void RetrieveCursorMetadata(DesktopFrame frame) {
			PointerInfo pointerInfo = new PointerInfo();

			// A non-zero mouse update timestamp indicates that there is a mouse position update and optionally a shape change
			if (frameInfo.LastMouseUpdateTime == 0)
				return;

			bool updatePosition = true;

			// Make sure we don't update pointer position wrongly
			// If pointer is invisible, make sure we did not get an update from another output that the last time that said pointer
			// was visible, if so, don't set it to invisible or update.

			if (!frameInfo.PointerPosition.Visible && (pointerInfo.WhoUpdatedPositionLast != this.mWhichOutputDevice))
				updatePosition = false;

			// If two outputs both say they have a visible, only update if new update has newer timestamp
			if (frameInfo.PointerPosition.Visible && pointerInfo.Visible && (pointerInfo.WhoUpdatedPositionLast != this.mWhichOutputDevice) && (pointerInfo.LastTimeStamp > frameInfo.LastMouseUpdateTime))
				updatePosition = false;

			// Update position
			if (updatePosition) {
				pointerInfo.Position = new SharpDX.Point(frameInfo.PointerPosition.Position.X, frameInfo.PointerPosition.Position.Y);
				pointerInfo.WhoUpdatedPositionLast = mWhichOutputDevice;
				pointerInfo.LastTimeStamp = frameInfo.LastMouseUpdateTime;
				pointerInfo.Visible = frameInfo.PointerPosition.Visible;
			}

			// No new shape
			if (frameInfo.PointerShapeBufferSize == 0)
				return;

			if (frameInfo.PointerShapeBufferSize > pointerInfo.BufferSize) {
				pointerInfo.PtrShapeBuffer = new byte[frameInfo.PointerShapeBufferSize];
				pointerInfo.BufferSize = frameInfo.PointerShapeBufferSize;
			}

			try {
				unsafe {
					fixed (byte* ptrShapeBufferPtr = pointerInfo.PtrShapeBuffer) {
						mDeskDupl.GetFramePointerShape(frameInfo.PointerShapeBufferSize, (IntPtr)ptrShapeBufferPtr, out pointerInfo.BufferSize, out pointerInfo.ShapeInfo);
					}
				}
			} catch (SharpDXException ex) {
				if (ex.ResultCode.Failure) {
					throw new DesktopDuplicationException("Failed to get frame pointer shape.");
				}
			}

			//frame.CursorVisible = pointerInfo.Visible;
			frame.CursorLocation = new System.Drawing.Point(pointerInfo.Position.X, pointerInfo.Position.Y);
		}

		private void ReleaseFrame() {
			try {
				mDeskDupl.ReleaseFrame();
			} catch (SharpDXException ex) {
				if (ex.ResultCode.Failure) {
					throw new DesktopDuplicationException("Failed to release frame.");
				}
			}
		}
	}
}
