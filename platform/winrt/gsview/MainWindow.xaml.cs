﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.ComponentModel;
using System.IO;
using System.Windows.Xps.Packaging;
using System.Printing;
using System.Windows.Markup;
using System.Runtime.InteropServices;

enum PDFType_t
{
	PDFX,
	PDFA
}

enum AppBar_t
{
	TEXT_SEARCH,
	STANDARD
}

enum NotifyType_t
{
	MESS_STATUS,
	MESS_ERROR
};

enum RenderingStatus_t
{
	REN_AVAILABLE,
	REN_THUMBS,
	REN_UPDATE_THUMB_CANVAS,
	REN_PAGE			/* Used to ignore value when source based setting */
};

public enum status_t
{
	S_ISOK,
	E_FAILURE,
	E_OUTOFMEM,
	E_NEEDPASSWORD
};

enum view_t
{
	VIEW_WEB,
	VIEW_CONTENT,
	VIEW_PAGE,
	VIEW_PASSWORD,
	VIEW_TEXTSEARCH
};

public enum Page_Content_t
{
	FULL_RESOLUTION = 0,
	THUMBNAIL,
	DUMMY,
	OLD_RESOLUTION,
	NOTSET
};

/* Put all the PDF types first to make the switch statment shorter 
   Save_Type_t.PDF is the test */
public enum Save_Type_t
{
	PDF13,
	PDFA1_RGB,
	PDFA1_CMYK,
	PDFA2_RGB,
	PDFA2_CMYK,
	PDFX3_GRAY,
	PDFX3_CMYK,
	PDF,
	PCLXL,
	XPS,
	SVG,
	PCLBitmap,
	PNG,
	PWG,
	PNM,
	TEXT
}

public enum Extract_Type_t
{
	PDF,
	EPS,
	PS,
	SVG
}

public struct spatial_info_t
{
	public Point size;
	public double scale_factor;
};

/* C# has no defines.... */
static class Constants
{
	public const int LOOK_AHEAD = 1;  /* A +/- count on the pages to pre-render */
	public const int THUMB_PREADD = 10;
	public const double MIN_SCALE = 0.5;
	public const double SCALE_THUMB = 0.05;
	public const int BLANK_WIDTH = 17;
	public const int BLANK_HEIGHT = 22;
	public const double ZOOM_STEP = 0.25;
	public const int ZOOM_MAX = 4;
	public const double ZOOM_MIN = 0.25;
	public const int KEY_PLUS = 0xbb;
	public const int KEY_MINUS = 0xbd;
	public const int ZOOM_IN = 0;
	public const int ZOOM_OUT = 1;
	public const double SCREEN_SCALE = 1;
	public const int HEADER_SIZE = 54;
	public const int SEARCH_FORWARD = 1;
	public const int SEARCH_BACKWARD = -1;
	public const int TEXT_NOT_FOUND = -1;
	public const int DEFAULT_GS_RES = 300;
	public const int DISPATCH_TIME = 50;
	public const int SCROLL_STEP = 10;
	public const int SCROLL_EDGE_BUFFER = 90;
}

public static class DocumentTypes
{
	public const string PDF = "Portable Document Format";
	public const string PS = "PostScript";
	public const string XPS = "XPS";
	public const string EPS = "Encapsulated PostScript";
	public const string CBZ = "Comic Book Archive";
	public const string UNKNOWN = "Unknown";
}

namespace gsview
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	/// 

	public struct ContextMenu_t
	{
		public int page_num;
		public Point mouse_position;
	}

	public struct thumb_t
	{
		public int page_num;
		public Byte[] bitmap;
		public Point size;
	}

	public struct searchResults_t
	{
		public String needle;
		public bool done;
		public int page_found;
		public List<Rect> rectangles;
		public int num_rects;
	}

	public struct textSelectInfo_t
	{
		public int pagenum;
		public bool first_line_full;
		public bool last_line_full;
	}

	public partial class MainWindow : Window
	{
		mudocument mu_doc;
		public Pages m_docPages;
		List<textSelectInfo_t> m_textSelect;
		List<DocPage> m_thumbnails;
		List<List<RectList>> m_page_link_list = null;
		IList<RectList> m_text_list;
		public List<LinesText> m_lineptrs = null;
		public List<BlocksText> m_textptrs = null;
		List<Boolean> m_textset = null;
		private bool m_file_open;
		private int m_currpage;
		private int m_searchpage;
		private int m_num_pages;
		private bool m_init_done;
		private bool m_links_on;
		String m_textsearchcolor = "#4072AC25";
		String m_textselectcolor = "#402572AC";
		String m_regionselect = "#00FFFFFF";
		//String m_regionselect = "#FFFF0000";  /* Debug */
		String m_linkcolor = "#40AC7225";
		private bool m_have_thumbs;
		double m_doczoom;
		ghostsharp m_ghostscript;
		String m_currfile;
		private gsprint m_ghostprint = null;
		bool m_isXPS;
		gsOutput m_gsoutput;
		Convert m_convertwin;
		Password m_password = null;
		BackgroundWorker m_thumbworker = null;
		BackgroundWorker m_textsearch = null;
		BackgroundWorker m_linksearch = null;
		String m_document_type;
		Info m_infowindow;
		OutputIntent m_outputintents;
		Selection m_selection;
		String m_prevsearch = null;
		int m_numpagesvisible;
		bool m_clipboardset;
		bool m_doscroll;
		bool m_intxtselect;
		bool m_textselected;
		System.Windows.Threading.DispatcherTimer m_dispatcherTimer = null;
		double m_lastY;
		double m_maxY;

		public MainWindow()
		{
			InitializeComponent();
			this.Closing += new System.ComponentModel.CancelEventHandler(Window_Closing);
			m_file_open = false;
			status_t result = CleanUp();

			/* Allocations and set up */
			try
			{
				m_docPages = new Pages();
				m_thumbnails = new List<DocPage>();
				m_lineptrs = new List<LinesText>();
				m_textptrs = new List<BlocksText>();
				m_textset = new List<Boolean>();
				m_ghostscript = new ghostsharp();
				m_ghostscript.gsUpdateMain += new ghostsharp.gsCallBackMain(gsProgress);
				m_gsoutput = new gsOutput();
				m_gsoutput.Activate();
				m_outputintents = new OutputIntent();
				m_outputintents.Activate();
				m_ghostscript.gsIOUpdateMain += new ghostsharp.gsIOCallBackMain(gsIO);
				m_convertwin = null;
				m_selection = null;
				xaml_ZoomSlider.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(ZoomReleased), true);

				mxaml_BackPage.Opacity = 0.5;
				mxaml_Contents.Opacity = 0.5;
				mxaml_currPage.Opacity = 0.5;
				mxaml_ForwardPage.Opacity = 0.5;
				mxaml_Links.Opacity = 0.5;
				mxaml_Print.Opacity = 0.5;
				mxaml_SavePDF.Opacity = 0.5;
				mxaml_Search.Opacity = 0.5;
				mxaml_Thumbs.Opacity = 0.5;
				mxaml_TotalPages.Opacity = 0.5;
				mxaml_zoomIn.Opacity = 0.5;
				mxaml_zoomOut.Opacity = 0.5;
				mxaml_Zoomsize.Opacity = 0.5;
				mxaml_Zoomsize.IsEnabled = false;
				xaml_ZoomSlider.Opacity = 0.5;
				xaml_ZoomSlider.IsEnabled = false;
			}
			catch (OutOfMemoryException e)
			{
				Console.WriteLine("Memory allocation failed at initialization\n");
				ShowMessage(NotifyType_t.MESS_ERROR, "Out of memory: " + e.Message);
			}
		}

		void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (m_selection != null && m_selection.IsActive)
				m_selection.Close();
			m_gsoutput.RealWindowClosing();
			m_outputintents.RealWindowClosing();
		}

		void EnabletoPDF()
		{
			xaml_savepdf13.IsEnabled = true;
			xaml_savepdfa.IsEnabled = true;
			xaml_savepdfx3_cmyk.IsEnabled = true;
			xaml_savepdfx3_gray.IsEnabled = true;
			xaml_savepclxl.IsEnabled = true;
		}

		void DisabletoPDF()
		{
			xaml_savepdf13.IsEnabled = false;
			xaml_savepdfa.IsEnabled = false;
			xaml_savepdfx3_cmyk.IsEnabled = false;
			xaml_savepdfx3_gray.IsEnabled = false;
			xaml_savepclxl.IsEnabled = false;
		}

		private status_t CleanUp()
		{
			m_init_done = false;
			this.Cursor = System.Windows.Input.Cursors.Arrow;
			/* Collapse this stuff since it is going to be released */
			xaml_ThumbGrid.Visibility = System.Windows.Visibility.Collapsed;
			xaml_ContentGrid.Visibility = System.Windows.Visibility.Collapsed;

			/* Clear out everything */
			if (m_docPages != null && m_docPages.Count > 0)
				m_docPages.Clear();
			if (m_textSelect != null)
				m_textSelect.Clear();
			if (m_textset != null)
				m_textset.Clear();
			if (m_lineptrs != null && m_lineptrs.Count > 0)
				m_lineptrs.Clear();
			if (m_thumbnails != null && m_thumbnails.Count > 0)
				m_thumbnails.Clear();
			if (m_textptrs != null && m_textptrs.Count > 0)
				m_textptrs.Clear();
			if (m_page_link_list != null && m_page_link_list.Count > 0)
			{
				m_page_link_list.Clear();
				m_page_link_list = null;
			}
			if (m_text_list != null && m_text_list.Count > 0)
			{
				m_text_list.Clear();
				m_text_list = null;
			}
			if (mu_doc != null)
				mu_doc.CleanUp();
			try
			{
				mu_doc = new mudocument();
			}
			catch (OutOfMemoryException e)
			{
				Console.WriteLine("Memory allocation failed during clean up\n");
				ShowMessage(NotifyType_t.MESS_ERROR, "Out of memory: " + e.Message);
			}
			status_t result = mu_doc.Initialize();

			if (result != status_t.S_ISOK)
			{
				Console.WriteLine("Library allocation failed during clean up\n");
				ShowMessage(NotifyType_t.MESS_ERROR, "Library allocation failed!");
				return result;
			}

			m_have_thumbs = false;
			m_file_open = false;
			m_num_pages = -1;
			m_links_on = false;
			m_doczoom = 1.0;
			m_isXPS = false;
			xaml_CancelThumb.IsEnabled = true;
			m_currpage = 0;
			m_document_type = DocumentTypes.UNKNOWN;
			EnabletoPDF();
			m_numpagesvisible = 3;
			m_clipboardset = false;
			m_doscroll = false;
			m_intxtselect = false;
			m_textselected = false;
			return result;
		}

		private void ShowMessage(NotifyType_t type, String Message)
		{
			if (type == NotifyType_t.MESS_ERROR)
			{
				System.Windows.Forms.MessageBox.Show(Message, "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			else
			{
				System.Windows.Forms.MessageBox.Show(Message, "Notice",
					MessageBoxButtons.OK);
			}
		}

		private void CloseDoc()
		{
			CleanUp();
		}

		/* Set the page with the new raster information */
		private void UpdatePage(int page_num, Byte[] bitmap, Point ras_size,
			Page_Content_t content, double zoom_in)
		{
			DocPage doc_page = this.m_docPages[page_num];

			doc_page.Width = (int)ras_size.X;
			doc_page.Height = (int)ras_size.Y;

			doc_page.Content = content;
			doc_page.Zoom = zoom_in;

			int stride = doc_page.Width * 4;
			doc_page.BitMap = BitmapSource.Create(doc_page.Width, doc_page.Height, 72, 72, PixelFormats.Pbgra32, BitmapPalettes.Halftone256, bitmap, stride);
			doc_page.PageNum = page_num;

			if (content == Page_Content_t.THUMBNAIL)
			{
				doc_page.Width = (int)(ras_size.X / Constants.SCALE_THUMB);
				doc_page.Height = (int)(ras_size.Y / Constants.SCALE_THUMB);
			}
		}

		private void OpenFile(object sender, RoutedEventArgs e)
		{
			if (m_password != null && m_password.IsActive)
				m_password.Close();

			if (m_infowindow != null && m_infowindow.IsActive)
				m_infowindow.Close();

			/* Check if gs is currently busy. If it is then don't allow a new
			 * file to be opened. They can cancel gs with the cancel button if
			 * they want */
			if (m_ghostscript.GetStatus() != gsStatus.GS_READY)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "GS busy. Cancel to open new file.");
				return;
			}

			if (m_ghostprint != null && m_ghostprint.IsBusy())
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "Let printing complete");
				return;
			}

			OpenFileDialog dlg = new OpenFileDialog();
			dlg.Filter = "Document Files(*.ps;*.eps;*.pdf;*.xps;*.cbz)|*.ps;*.eps;*.pdf;*.xps;*.cbz|All files (*.*)|*.*";
			dlg.FilterIndex = 1;
			if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				if (m_file_open)
				{
					CloseDoc();
				}
				/* If we have a ps or eps file then launch the distiller first
				 * and then we will get a temp pdf file which will be opened by
				 * mupdf */
				string extension = System.IO.Path.GetExtension(dlg.FileName);
				/* We are doing this based on the extension but like should do
				 * it based upon the content */
				switch (extension.ToUpper())
				{
					case ".PS":
						m_document_type = DocumentTypes.PS;
						break;
					case ".EPS":
						m_document_type = DocumentTypes.EPS;
						break;
					case ".XPS":
						m_document_type = DocumentTypes.XPS;
						break;
					case ".PDF":
						m_document_type = DocumentTypes.PDF;
						break;
					case ".CBZ":
						m_document_type = DocumentTypes.CBZ;
						break;
					default:
						m_document_type = DocumentTypes.UNKNOWN;
						break;
				}
				if (extension.ToUpper() == ".PS" || extension.ToUpper() == ".EPS")
				{
					xaml_DistillProgress.Value = 0;
					if (m_ghostscript.DistillPS(dlg.FileName, Constants.DEFAULT_GS_RES) == gsStatus.GS_BUSY)
					{
						ShowMessage(NotifyType_t.MESS_STATUS, "GS currently busy");
						return;
					}
					xaml_DistillName.Text = "Distilling";
					xaml_CancelDistill.Visibility = System.Windows.Visibility.Visible;
					xaml_DistillName.FontWeight = FontWeights.Bold;
					xaml_DistillGrid.Visibility = System.Windows.Visibility.Visible;
					return;
				}
				/* Set if this is already xps for printing */
				if (extension.ToUpper() == ".XPS")
				{
					DisabletoPDF();
					m_isXPS = true;
				}
				OpenFile2(dlg.FileName);
			}
		}

		private void OpenFile2(String File)
		{
			m_currfile = File;

			status_t code = mu_doc.OpenFile(m_currfile);
			if (code == status_t.S_ISOK)
			{
				/* Check if we need a password */
				if (mu_doc.RequiresPassword())
					GetPassword();
				else
					StartViewer();
			}
			else
			{
				m_currfile = null;
				ShowMessage(NotifyType_t.MESS_ERROR, "Failed to open file!");
			}
		}

		private void StartViewer()
		{
			InitialRender();
			RenderThumbs();
			m_file_open = true;
			mxaml_BackPage.Opacity = 1;
			mxaml_Contents.Opacity = 1;
			mxaml_currPage.Opacity = 1;
			mxaml_ForwardPage.Opacity = 1;
			mxaml_Links.Opacity = 1;
			mxaml_Print.Opacity = 1;
			mxaml_SavePDF.Opacity = 1;
			mxaml_Search.Opacity = 1;
			mxaml_Thumbs.Opacity = 1;
			mxaml_TotalPages.Opacity = 1;
			mxaml_zoomIn.Opacity = 1;
			mxaml_zoomOut.Opacity = 1;
			mxaml_Zoomsize.Opacity = 1;
			mxaml_Zoomsize.IsEnabled = true;
			mxaml_TotalPages.Text = "/ " + m_num_pages.ToString();
			mxaml_currPage.Text = "1";
			xaml_ZoomSlider.Opacity = 1.0;
			xaml_ZoomSlider.IsEnabled = true;
		}

		private status_t ComputePageSize(int page_num, double scale_factor,
									out Point render_size)
		{
			Point renpageSize = new Point();

			status_t code = (status_t)mu_doc.GetPageSize(page_num, out render_size);
			if (code != status_t.S_ISOK)
				return code;

			renpageSize.X = (render_size.X * scale_factor);
			renpageSize.Y = (render_size.Y * scale_factor);

			render_size = renpageSize;

			return status_t.S_ISOK;
		}

		private DocPage InitDocPage()
		{
			DocPage doc_page = new DocPage();

			doc_page.BitMap = null;
			doc_page.Height = Constants.BLANK_HEIGHT;
			doc_page.Width = Constants.BLANK_WIDTH;
			doc_page.NativeHeight = Constants.BLANK_HEIGHT;
			doc_page.NativeWidth = Constants.BLANK_WIDTH;
			doc_page.Content = Page_Content_t.DUMMY;
			doc_page.TextBox = null;
			doc_page.LinkBox = null;
			doc_page.SelHeight = 0;
			doc_page.SelWidth = 0;
			doc_page.SelX = 0;
			doc_page.SelY = 0;
			return doc_page;
		}

		async private void InitialRender()
		{
			m_num_pages = mu_doc.GetPageCount();
			m_currpage = 0;

			for (int k = 0; k < m_num_pages; k++)
			{
				m_docPages.Add(InitDocPage());
				m_docPages[k].PageNum = k;
				m_thumbnails.Add(InitDocPage());
				m_textptrs.Add(new BlocksText());
				m_lineptrs.Add(new LinesText());
				m_textset.Add(false);
			}

			/* Do the first few full res pages */
			for (int k = 0; k < Constants.LOOK_AHEAD + 2; k++)
			{
				if (m_num_pages > k)
				{
					Point ras_size;
					double scale_factor = 1.0;

					if (ComputePageSize(k, scale_factor, out ras_size) == status_t.S_ISOK)
					{
						try
						{
							Byte[] bitmap = new byte[(int)ras_size.X * (int)ras_size.Y * 4];
							BlocksText charlist = null;

							Task<int> ren_task =
								new Task<int>(() => mu_doc.RenderPage(k, bitmap,
									(int)ras_size.X, (int)ras_size.Y, scale_factor,
									false, true, !(m_textset[k]), out charlist));
							ren_task.Start();
							await ren_task.ContinueWith((antecedent) =>
							{
								status_t code = (status_t)ren_task.Result;
								if (code == status_t.S_ISOK)
								{
									m_textset[k] = true;
									m_textptrs[k] = charlist;
									m_docPages[k].TextBlocks = charlist;
									UpdatePage(k, bitmap, ras_size,
										Page_Content_t.FULL_RESOLUTION, 1.0);
								}
							}, TaskScheduler.FromCurrentSynchronizationContext());
						}
						catch (OutOfMemoryException e)
						{
							Console.WriteLine("Memory allocation failed page " + k + "\n");
							ShowMessage(NotifyType_t.MESS_ERROR, "Out of memory: " + e.Message);
						}
					}
				}
			}
			m_init_done = true;
			m_currpage = 0;
			xaml_PageList.ItemsSource = m_docPages;
		}

		private void OnBackPageClick(object sender, RoutedEventArgs e)
		{
			if (m_currpage == 0 || !m_init_done) return;
			RenderRange(m_currpage - 1, true);
		}

		private void OnForwardPageClick(object sender, RoutedEventArgs e)
		{
			if (m_currpage == m_num_pages - 1 || !m_init_done) return;
			RenderRange(m_currpage + 1, true);
		}

		private void PageEnterClicked(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				var desired_page = mxaml_currPage.Text;
				try
				{
					int page = System.Convert.ToInt32(desired_page);
					if (page > 0 && page < (m_num_pages + 1))
						RenderRange(page - 1, true);
				}
				catch (FormatException e1)
				{
					Console.WriteLine("String is not a sequence of digits.");
				}
				catch (OverflowException e2)
				{
					Console.WriteLine("The number cannot fit in an Int32.");
				}
			}
		}

		private void CancelLoadClick(object sender, RoutedEventArgs e)
		{
			/* Cancel during thumbnail loading. Deactivate the button 
			 * and cancel the thumbnail rendering */
			if (m_thumbworker != null)
				m_thumbworker.CancelAsync();
			xaml_CancelThumb.IsEnabled = false;
		}

		private void ToggleThumbs(object sender, RoutedEventArgs e)
		{
			if (m_have_thumbs)
			{
				if (xaml_ThumbGrid.Visibility == System.Windows.Visibility.Collapsed)
				{
					xaml_ThumbGrid.Visibility = System.Windows.Visibility.Visible;
				}
				else
				{
					xaml_ThumbGrid.Visibility = System.Windows.Visibility.Collapsed;
				}
			}
		}

		private void ToggleContents(object sender, RoutedEventArgs e)
		{
			if (xaml_ContentGrid.Visibility == System.Windows.Visibility.Visible)
			{
				xaml_ContentGrid.Visibility = System.Windows.Visibility.Collapsed;
				return;
			}

			if (m_num_pages < 0)
				return;

			if (xaml_ContentList.Items.IsEmpty)
			{
				int size_content = mu_doc.ComputeContents();
				if (size_content == 0)
					return;
				xaml_ContentList.ItemsSource = mu_doc.contents;
			}
			xaml_ContentGrid.Visibility = System.Windows.Visibility.Visible;
		}

		private void ThumbSelected(object sender, MouseButtonEventArgs e)
		{
			var item = ((FrameworkElement)e.OriginalSource).DataContext as DocPage;
			if (item != null)
			{
				if (item.PageNum < 0)
					return;
				RenderRange(item.PageNum, true);
			}
		}

		private void ContentSelected(object sender, MouseButtonEventArgs e)
		{
			var item = ((FrameworkElement)e.OriginalSource).DataContext as ContentItem;
			if (item != null && item.Page < m_num_pages)
			{
				int page = m_docPages[item.Page].PageNum;
				if (page >= 0 && page < m_num_pages)
					RenderRange(page, true);
			}
		}

		/* We need to avoid rendering due to size changes */
		private void ListViewScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			var lv = (System.Windows.Controls.ListView)sender;
			foreach (var lvi in lv.Items)
			{
				var container = lv.ItemContainerGenerator.ContainerFromItem(lvi) as ListBoxItem;
				if (container != null && Visible(container, lv))
				{
					var found = container.Content;
					if (found != null)
					{
						var Item = (DocPage)found;
						if (!(m_dispatcherTimer != null && m_dispatcherTimer.IsEnabled == true))
							RenderRange(Item.PageNum, false);
					}
					e.Handled = true;
					return;
				}
			}
		}

		/* Render +/- the look ahead from where we are if blank page is present */
		async private void RenderRange(int new_page, bool scrollto)
		{
			int range = (int)Math.Ceiling(((double)m_numpagesvisible - 1.0) / 2.0);

			for (int k = new_page - range; k <= new_page + range + 1; k++)
			{
				if (k >= 0 && k < m_num_pages)
				{
					/* Check if page is already rendered */
					var doc = m_docPages[k];
					if (doc.Content != Page_Content_t.FULL_RESOLUTION ||
						doc.Zoom != m_doczoom)
					{
						Point ras_size;
						double scale_factor = m_doczoom;
						/* To avoid multiple page renderings on top of one 
						 * another with scroll changes mark this as being 
						 * full resolution */
						m_docPages[k].Content = Page_Content_t.FULL_RESOLUTION;

						if (ComputePageSize(k, scale_factor, out ras_size) == status_t.S_ISOK)
						{
							try
							{
								Byte[] bitmap = new byte[(int)ras_size.X * (int)ras_size.Y * 4];
								BlocksText charlist = null;

								Task<int> ren_task =
									new Task<int>(() => mu_doc.RenderPage(k, bitmap,
										(int)ras_size.X, (int)ras_size.Y, scale_factor,
										false, true, !(m_textset[k]), out charlist));
								ren_task.Start();
								await ren_task.ContinueWith((antecedent) =>
								{
									status_t code = (status_t)ren_task.Result;
									if (code == status_t.S_ISOK)
									{
										if (m_docPages[k].TextBox != null)
											ScaleTextBox(k);
										if (m_links_on && m_page_link_list != null)
										{
											m_docPages[k].LinkBox = m_page_link_list[k];
											if (m_docPages[k].LinkBox != null)
												ScaleLinkBox(k);
										}
										else
										{
											m_docPages[k].LinkBox = null;
										}
										if (!(m_textset[k]) && charlist != null)
										{
											m_textptrs[k] = charlist;
											if (scale_factor != 1.0)
												ScaleTextBlocks(k, scale_factor);
											m_docPages[k].TextBlocks = m_textptrs[k];
											m_textset[k] = true;
										}
										else
										{
											/* We had to rerender due to scale */
											if (m_textptrs[k] != null)
											{
												ScaleTextBlocks(k, scale_factor);
												m_docPages[k].TextBlocks = m_textptrs[k];
											}
											if (m_lineptrs[k] != null)
											{
												ScaleTextLines(k, scale_factor);
												m_docPages[k].SelectedLines = m_lineptrs[k];
											}
										}
										UpdatePage(k, bitmap, ras_size,
											Page_Content_t.FULL_RESOLUTION, m_doczoom);
										if (k == new_page && scrollto)
										{
											m_doscroll = true;
											xaml_PageList.ScrollIntoView(m_docPages[k]);
										}
									}
								}, TaskScheduler.FromCurrentSynchronizationContext());
							}
							catch (OutOfMemoryException e)
							{
								Console.WriteLine("Memory allocation failed page " + k + "\n");
								ShowMessage(NotifyType_t.MESS_ERROR, "Out of memory: " + e.Message);
							}
						}
					}
					else
					{
						/* We did not have to render the page but we may need to
						 * scroll to it */
						if (k == new_page && scrollto)
						{
							m_doscroll = true;
							xaml_PageList.ScrollIntoView(m_docPages[k]);
						}
						/*
						if (k == new_page && m_docPages[k].TextBlocks == null)
						{
							m_docPages[k].TextBlocks = m_textptrs[k];
						} */
					}
				}
			}
			/* Release old range and set new page */
			ReleasePages(m_currpage, new_page, range);
			m_currpage = new_page;
			mxaml_currPage.Text = (m_currpage + 1).ToString();
		}

		private bool Visible(FrameworkElement elem, FrameworkElement cont)
		{
			if (!elem.IsVisible)
				return false;
			Rect rect = new Rect(0.0, 0.0, cont.ActualWidth, cont.ActualHeight);
			Rect bounds = elem.TransformToAncestor(cont).TransformBounds(new Rect(0.0, 0.0, elem.ActualWidth, elem.ActualHeight));
			Rect bounds2 = new Rect(new Point(bounds.TopLeft.X, bounds.TopLeft.Y), new Point(bounds.BottomRight.X, bounds.BottomRight.Y - 5));
			return rect.Contains(bounds2.TopLeft) || rect.Contains(bounds2.BottomRight);
		}

		/* Avoids the next page jumping into view when touched by mouse */
		private void AvoidScrollIntoView(object sender, RequestBringIntoViewEventArgs e)
		{
			if (!m_doscroll)
				e.Handled = true;
			else
				m_doscroll = false;
		}

		private void ReleasePages(int old_page, int new_page, int range)
		{
			if (old_page == new_page) return;
			/* To keep from having memory issue reset the page back to
				the thumb if we are done rendering the thumbnails */
			for (int k = old_page - range; k <= old_page + range; k++)
			{
				if (k < new_page - range || k > new_page + range)
				{
					if (k >= 0 && k < m_num_pages)
					{
						SetThumb(k);
					}
				}
			}
		}

		/* Return this page from a full res image to the thumb image */
		private void SetThumb(int page_num)
		{
			/* See what is there now */
			var doc_page = m_docPages[page_num];
			if (doc_page.Content == Page_Content_t.THUMBNAIL &&
				doc_page.Zoom == m_doczoom) return;

			if (m_thumbnails.Count > page_num)
			{
				doc_page.Content = Page_Content_t.THUMBNAIL;
				doc_page.Zoom = m_doczoom;

				doc_page.BitMap = m_thumbnails[page_num].BitMap;
				doc_page.Width = (int)(m_doczoom * doc_page.BitMap.PixelWidth / Constants.SCALE_THUMB);
				doc_page.Height = (int)(m_doczoom * doc_page.BitMap.PixelHeight / Constants.SCALE_THUMB);
				doc_page.PageNum = page_num;
				doc_page.LinkBox = null;
				doc_page.TextBox = null;
				/* No need to refresh unless it just occurs during other stuff
				 * we just want to make sure we can release the bitmaps */
				//doc_page.PageRefresh();
			}
		}

		private void gsIO(object gsObject, String mess, int len)
		{
			m_gsoutput.Update(mess, len);
		}

		private void gsProgress(object gsObject, gsEventArgs asyncInformation)
		{
			if (asyncInformation.Completed)
			{
				xaml_DistillProgress.Value = 100;
				xaml_DistillGrid.Visibility = System.Windows.Visibility.Collapsed;
				if (asyncInformation.Params.result == GS_Result_t.gsFAILED)
				{
					switch (asyncInformation.Params.task)
					{
						case GS_Task_t.CREATE_XPS:
							ShowMessage(NotifyType_t.MESS_STATUS, "Ghostscript failed to create XPS");
							break;

						case GS_Task_t.PS_DISTILL:
							ShowMessage(NotifyType_t.MESS_STATUS, "Ghostscript failed to distill PS");
							break;

						case GS_Task_t.SAVE_RESULT:
							ShowMessage(NotifyType_t.MESS_STATUS, "Ghostscript failed to convert document");
							break;
					}
					return;
				}
				GSResult(asyncInformation.Params);
			}
			else
			{
				this.xaml_DistillProgress.Value = asyncInformation.Progress;
			}
		}

		/* GS Result*/
		public void GSResult(gsParams_t gs_result)
		{
			if (gs_result.result == GS_Result_t.gsCANCELLED)
			{
				xaml_DistillGrid.Visibility = System.Windows.Visibility.Collapsed;
				return;
			}
			if (gs_result.result == GS_Result_t.gsFAILED)
			{
				xaml_DistillGrid.Visibility = System.Windows.Visibility.Collapsed;
				ShowMessage(NotifyType_t.MESS_STATUS, "GS Failed Conversion");
				return;
			}
			switch (gs_result.task)
			{
				case GS_Task_t.CREATE_XPS:
					xaml_DistillGrid.Visibility = System.Windows.Visibility.Collapsed;
					PrintXPS(gs_result.outputfile);
					break;

				case GS_Task_t.PS_DISTILL:
					xaml_DistillGrid.Visibility = System.Windows.Visibility.Collapsed;
					OpenFile2(gs_result.outputfile);
					break;

				case GS_Task_t.SAVE_RESULT:
					ShowMessage(NotifyType_t.MESS_STATUS, "GS Completed Conversion");
					break;
			}
		}

		/* Printing is achieved using xpswrite device in ghostscript and
		 * pushing that file through the XPS print queue */
		private void Print(object sender, RoutedEventArgs e)
		{
			if (!m_file_open)
				return;

			/* If file is already xps then gs need not do this */
			if (!m_isXPS)
			{
				xaml_DistillProgress.Value = 0;
				if (m_ghostscript.CreateXPS(m_currfile, Constants.DEFAULT_GS_RES, m_num_pages) == gsStatus.GS_BUSY)
				{
					ShowMessage(NotifyType_t.MESS_STATUS, "GS currently busy");
					return;
				}
				else
				{
					/* Right now this is not possible to cancel due to the way 
					 * that gs is run for xpswrite from pdf */
					xaml_CancelDistill.Visibility = System.Windows.Visibility.Collapsed;
					xaml_DistillName.Text = "Convert to XPS";
					xaml_DistillName.FontWeight = FontWeights.Bold;
					xaml_DistillGrid.Visibility = System.Windows.Visibility.Visible;
				}
			}
			else
				PrintXPS(m_currfile);
		}

		private void PrintXPS(String file)
		{
			gsprint ghostprint = new gsprint();
			System.Windows.Controls.PrintDialog pDialog = ghostprint.GetPrintDialog();

			if (pDialog == null)
				return;
			/* We have to create the XPS document on a different thread */
			XpsDocument xpsDocument = new XpsDocument(file, FileAccess.Read);
			FixedDocumentSequence fixedDocSeq = xpsDocument.GetFixedDocumentSequence();
			PrintQueue printQueue = pDialog.PrintQueue;

			m_ghostprint = ghostprint;
			xaml_PrintGrid.Visibility = System.Windows.Visibility.Visible;

			xaml_PrintProgress.Value = 0;

			ghostprint.Print(printQueue, fixedDocSeq);
		}

		private void PrintProgress(object printHelper, gsPrintEventArgs Information)
		{
			if (Information.Status != PrintStatus_t.PRINT_BUSY)
			{
				xaml_PrintProgress.Value = 100;
				xaml_PrintGrid.Visibility = System.Windows.Visibility.Collapsed;
			}
			else
			{
				xaml_PrintProgress.Value =
					100.0 * (double)Information.Page / (double)m_num_pages;
			}
		}

		private void CancelDistillClick(object sender, RoutedEventArgs e)
		{
			xaml_CancelDistill.IsEnabled = false;
			if (m_ghostscript != null)
				m_ghostscript.Cancel();
		}

		private void CancelPrintClick(object sender, RoutedEventArgs e)
		{
			m_ghostprint.CancelAsync();
		}

		private void ShowGSMessage(object sender, RoutedEventArgs e)
		{
			m_gsoutput.Show();
		}

		private void ConvertClick(object sender, RoutedEventArgs e)
		{
			if (m_ghostscript.GetStatus() != gsStatus.GS_READY)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "GS busy");
				return;
			}

			if (m_convertwin == null || !m_convertwin.IsActive)
			{
				m_convertwin = new Convert(m_num_pages);
				m_convertwin.ConvertUpdateMain += new Convert.ConvertCallBackMain(ConvertReturn);
				m_convertwin.Activate();
				m_convertwin.Show();
			}
		}

		private void ConvertReturn(object sender)
		{
			if (m_ghostscript.GetStatus() != gsStatus.GS_READY)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "GS busy");
				return;
			}

			Device device = (Device)m_convertwin.xaml_DeviceList.SelectedItem;
			System.Collections.IList pages = m_convertwin.xaml_PageList.SelectedItems;
			System.Collections.IList pages_selected = null;
			String options = m_convertwin.xaml_options.Text;
			int resolution = 72;
			bool multi_page_needed = false;
			int first_page = -1;
			int last_page = -1;

			if (pages.Count == 0)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "No Pages Selected");
				return;
			}

			if (device == null)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "No Device Selected");
				return;
			}

			/* Get a filename */
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Filter = "All files (*.*)|*.*";
			dlg.FilterIndex = 1;
			if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				if (!device.SupportsMultiPage && m_num_pages > 1)
					multi_page_needed = true;

				if (pages.Count != m_num_pages)
				{
					/* We may need to go through page by page. Determine if
					 * selection of pages is continuous.  This is done by 
					 * looking at the first one in the list and the last one
					 * in the list and checking the length */
					SelectPage lastpage = (SelectPage)pages[pages.Count - 1];
					SelectPage firstpage = (SelectPage)pages[0];
					int temp = lastpage.Page - firstpage.Page + 1;
					if (temp == pages.Count)
					{
						/* Pages are contiguous.  Add first and last page 
						 * as command line option */
						options = options + " -dFirstPage=" + firstpage.Page + " -dLastPage=" + lastpage.Page;
						first_page = firstpage.Page;
						last_page = lastpage.Page;
					}
					else
					{
						/* Pages are not continguous.  We will do this page 
						 * by page.*/
						pages_selected = pages;
						multi_page_needed = true;  /* need to put in separate outputs */
					}
				}
				xaml_DistillProgress.Value = 0;
				if (m_ghostscript.Convert(m_currfile, options,
					device.DeviceName, dlg.FileName, pages.Count, resolution,
					multi_page_needed, pages_selected, first_page, last_page,
					null, null) == gsStatus.GS_BUSY)
				{
					ShowMessage(NotifyType_t.MESS_STATUS, "GS busy");
					return;
				}
				xaml_DistillName.Text = "GS Converting Document";
				xaml_CancelDistill.Visibility = System.Windows.Visibility.Collapsed;
				xaml_DistillName.FontWeight = FontWeights.Bold;
				xaml_DistillGrid.Visibility = System.Windows.Visibility.Visible;
				m_convertwin.Close();
			}
			return;
		}

		private void GetPassword()
		{
			if (m_password == null)
			{
				m_password = new Password();
				m_password.PassUpdateMain += new Password.PassCallBackMain(PasswordReturn);
				m_password.Activate();
				m_password.Show();
			}
		}

		private void PasswordReturn(object sender)
		{
			if (mu_doc.ApplyPassword(m_password.xaml_Password.Password))
			{
				m_password.Close();
				m_password = null;
				StartViewer();
			}
			else
				ShowMessage(NotifyType_t.MESS_STATUS, "Password Incorrect");
		}

		private void ShowInfo(object sender, RoutedEventArgs e)
		{
			String Message;

			if (m_file_open)
			{
				Message =
					"         File: " + m_currfile + "\n" +
					"Document Type: " + m_document_type + "\n" +
					"        Pages: " + m_num_pages + "\n" +
					" Current Page: " + (m_currpage + 1) + "\n";
				if (m_infowindow == null || !(m_infowindow.IsActive))
					m_infowindow = new Info();
				m_infowindow.xaml_TextInfo.Text = Message;
				m_infowindow.FontFamily = new FontFamily("Courier New");
				m_infowindow.Show();
			}
		}

		#region Zoom Control
		private void ZoomOut(object sender, RoutedEventArgs e)
		{
			if (!m_init_done)
				return;
			m_doczoom = m_doczoom - Constants.ZOOM_STEP;
			if (m_doczoom < Constants.ZOOM_MIN)
				m_doczoom = Constants.ZOOM_MIN;
			xaml_ZoomSlider.Value = m_doczoom * 100.0;
			m_numpagesvisible = (int)(Math.Ceiling((1.0 / m_doczoom) + 2));
			RenderRange(m_currpage, false);
		}

		private void ZoomIn(object sender, RoutedEventArgs e)
		{
			if (!m_init_done)
				return;
			m_doczoom = m_doczoom + Constants.ZOOM_STEP;
			if (m_doczoom > Constants.ZOOM_MAX)
				m_doczoom = Constants.ZOOM_MAX;
			xaml_ZoomSlider.Value = m_doczoom * 100.0;
			m_numpagesvisible = (int)(Math.Ceiling((1.0 / m_doczoom) + 2));
			RenderRange(m_currpage, false);
		}

		private void ShowFooter(object sender, RoutedEventArgs e)
		{
			xaml_FooterControl.Visibility = System.Windows.Visibility.Visible;
		}

		private void HideFooter(object sender, RoutedEventArgs e)
		{
			xaml_FooterControl.Visibility = System.Windows.Visibility.Collapsed;
		}

		private void ZoomReleased(object sender, MouseButtonEventArgs e)
		{
			if (m_init_done)
			{
				double zoom = xaml_ZoomSlider.Value / 100.0;
				if (zoom > Constants.ZOOM_MAX)
					zoom = Constants.ZOOM_MAX;
				if (zoom < Constants.ZOOM_MIN)
					zoom = Constants.ZOOM_MIN;
				m_numpagesvisible = (int)(Math.Ceiling((1.0 / zoom) + 2));
				m_doczoom = zoom;
				RenderRange(m_currpage, false);
			}
		}

		/* If the zoom is not equalto 1 then set the zoom to 1 and scoll to this page */
		private void PageDoubleClick(object sender, MouseButtonEventArgs e)
		{
			return; /* Disable this for now */
			if (m_doczoom != 1.0)
			{
				m_doczoom = 1.0;
				mxaml_Zoomsize.Text = "100";
				var item = ((FrameworkElement)e.OriginalSource).DataContext as DocPage;
				if (item != null)
					RenderRange(item.PageNum, true);
			}
		}

		private void ZoomEnterClicked(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				var desired_zoom = mxaml_Zoomsize.Text;
				try
				{
					double zoom = (double)System.Convert.ToInt32(desired_zoom) / 100.0;
					if (zoom > Constants.ZOOM_MAX)
						zoom = Constants.ZOOM_MAX;
					if (zoom < Constants.ZOOM_MIN)
						zoom = Constants.ZOOM_MIN;
					m_numpagesvisible = (int)(Math.Ceiling((1.0 / zoom) + 2));
					m_doczoom = zoom;
					RenderRange(m_currpage, false);
				}
				catch (FormatException e1)
				{
					Console.WriteLine("String is not a sequence of digits.");
				}
				catch (OverflowException e2)
				{
					Console.WriteLine("The number cannot fit in an Int32.");
				}
			}
		}

		#endregion Zoom Control

		#region Thumb Rendering
		void SetThumbInit(int page_num, Byte[] bitmap, Point ras_size, double zoom_in)
		{
			/* Two jobs. Store the thumb and possibly update the full page */
			DocPage doc_page = m_thumbnails[page_num];

			doc_page.Width = (int)ras_size.X;
			doc_page.Height = (int)ras_size.Y;
			doc_page.Content = Page_Content_t.THUMBNAIL;
			doc_page.Zoom = zoom_in;
			int stride = doc_page.Width * 4;
			doc_page.BitMap = BitmapSource.Create(doc_page.Width, doc_page.Height, 72, 72, PixelFormats.Pbgra32, BitmapPalettes.Halftone256, bitmap, stride);
			doc_page.PageNum = page_num;

			/* And the main page */
			var doc = m_docPages[page_num];
			if (doc.Content == Page_Content_t.THUMBNAIL || doc.Content == Page_Content_t.FULL_RESOLUTION)
				return;
			else
			{
				doc_page = this.m_docPages[page_num];
				doc_page.Content = Page_Content_t.THUMBNAIL;
				doc_page.Zoom = zoom_in;

				doc_page.BitMap = m_thumbnails[page_num].BitMap;
				doc_page.Width = (int)(ras_size.X / Constants.SCALE_THUMB);
				doc_page.Height = (int)(ras_size.Y / Constants.SCALE_THUMB);
				doc_page.PageNum = page_num;
			}
		}

		private void ThumbsWork(object sender, DoWorkEventArgs e)
		{
			Point ras_size;
			status_t code;
			double scale_factor = Constants.SCALE_THUMB;
			BackgroundWorker worker = sender as BackgroundWorker;

			Byte[] bitmap;

			for (int k = 0; k < m_num_pages; k++)
			{
				if (ComputePageSize(k, scale_factor, out ras_size) == status_t.S_ISOK)
				{
					try
					{
						bitmap = new byte[(int)ras_size.X * (int)ras_size.Y * 4];
						BlocksText charlist;

						/* Synchronous call on our background thread */
						code = (status_t)mu_doc.RenderPage(k, bitmap, (int)ras_size.X,
							(int)ras_size.Y, scale_factor, false, false, false,
							out charlist);
					}
					catch (OutOfMemoryException em)
					{
						Console.WriteLine("Memory allocation failed thumb page " + k + em.Message + "\n");
						break;
					}
					/* Use thumb if we rendered ok */
					if (code == status_t.S_ISOK)
					{
						double percent = 100 * (double)(k + 1) / (double)m_num_pages;
						thumb_t curr_thumb = new thumb_t();
						curr_thumb.page_num = k;
						curr_thumb.bitmap = bitmap;
						curr_thumb.size = ras_size;
						worker.ReportProgress((int)percent, curr_thumb);
					}
				}
				if (worker.CancellationPending == true)
				{
					e.Cancel = true;
					break;
				}
			}
		}

		private void ThumbsCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			xaml_ProgressGrid.Visibility = System.Windows.Visibility.Collapsed;
			xaml_ThumbProgress.Value = 0;
			xaml_ThumbList.ItemsSource = m_thumbnails;
			m_have_thumbs = true;
			m_thumbworker = null;
			xaml_CancelThumb.IsEnabled = true;
		}

		private void ThumbsProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			thumb_t thumb = (thumb_t)(e.UserState);

			xaml_ThumbProgress.Value = e.ProgressPercentage;
			SetThumbInit(thumb.page_num, thumb.bitmap, thumb.size, 1.0);
		}

		private void RenderThumbs()
		{
			/* Create background task for rendering the thumbnails.  Allow
			this to be cancelled if we open a new doc while we are in loop
			rendering.  Put the UI updates in the progress changed which will
			run on the main thread */
			try
			{
				m_thumbworker = new BackgroundWorker();
				m_thumbworker.WorkerReportsProgress = true;
				m_thumbworker.WorkerSupportsCancellation = true;
				m_thumbworker.DoWork += new DoWorkEventHandler(ThumbsWork);
				m_thumbworker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ThumbsCompleted);
				m_thumbworker.ProgressChanged += new ProgressChangedEventHandler(ThumbsProgressChanged);
				xaml_ProgressGrid.Visibility = System.Windows.Visibility.Visible;
				m_thumbworker.RunWorkerAsync();
			}
			catch (OutOfMemoryException e)
			{
				Console.WriteLine("Memory allocation failed during thumb rendering\n");
				ShowMessage(NotifyType_t.MESS_ERROR, "Out of memory: " + e.Message);
			}
		}
		#endregion Thumb Rendering

		#region Copy Paste
		/* Copy the current page as a bmp to the clipboard this is done at the 
		 * current resolution */
		private void CopyPage(object sender, RoutedEventArgs e)
		{
			if (!m_init_done)
				return;
			var curr_page = m_docPages[m_currpage];
			System.Windows.Clipboard.SetImage(curr_page.BitMap);
			m_clipboardset = true;
		}

		/* Paste the page to various types supported by the windows encoder class */
		private void PastePage(object sender, RoutedEventArgs e)
		{
			var menu = (System.Windows.Controls.MenuItem)sender;

			String tag = (String)menu.Tag;

			if (!m_clipboardset || !System.Windows.Clipboard.ContainsImage() ||
				!m_init_done)
				return;
			var bitmap = System.Windows.Clipboard.GetImage();

			BitmapEncoder encoder;
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.FilterIndex = 1;

			switch (tag)
			{
				case "PNG":
					dlg.Filter = "PNG Files(*.png)|*.png";
					encoder = new PngBitmapEncoder();

					break;
				case "JPG":
					dlg.Filter = "JPEG Files(*.jpg)|*.jpg";
					encoder = new JpegBitmapEncoder();
					break;

				case "WDP":
					dlg.Filter = "HDP Files(*.wdp)|*.wdp";
					encoder = new WmpBitmapEncoder();
					break;

				case "TIF":
					dlg.Filter = "TIFF Files(*.tif)|*.tif";
					encoder = new TiffBitmapEncoder();
					break;

				case "BMP":
					dlg.Filter = "BMP Files(*.bmp)|*.bmp";
					encoder = new BmpBitmapEncoder();
					break;

				case "GIF":
					dlg.Filter = "GIF Files(*.gif)|*.gif";
					encoder = new GifBitmapEncoder();
					break;

				default:
					return;
			}

			encoder.Frames.Add(BitmapFrame.Create(bitmap));
			if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				using (var stream = dlg.OpenFile())
					encoder.Save(stream);
			}
		}
		#endregion Copy Paste

		#region SaveAs
		String CreatePDFXA(Save_Type_t type)
		{
			Byte[] Resource;
			String Profile;

			switch (type)
			{
				case Save_Type_t.PDFA1_CMYK:
				case Save_Type_t.PDFA2_CMYK:
					Resource = Properties.Resources.PDFA_def;
					Profile = m_outputintents.cmyk_icc;
					break;

				case Save_Type_t.PDFA1_RGB:
				case Save_Type_t.PDFA2_RGB:
					Resource = Properties.Resources.PDFA_def;
					Profile = m_outputintents.rgb_icc;
					break;

				case Save_Type_t.PDFX3_CMYK:
					Resource = Properties.Resources.PDFX_def;
					Profile = m_outputintents.cmyk_icc;
					break;

				case Save_Type_t.PDFX3_GRAY:
					Resource = Properties.Resources.PDFX_def;
					Profile = m_outputintents.gray_icc;
					break;

				default:
					return null;
			}

			String Profile_new = Profile.Replace("\\", "/");
			String result = System.Text.Encoding.UTF8.GetString(Resource);
			String pdfx_cust = result.Replace("ICCPROFILE", Profile_new);
			var out_file = System.IO.Path.GetTempFileName();
			System.IO.File.WriteAllText(out_file, pdfx_cust);
			return out_file;
		}

		private void SaveFile(Save_Type_t type)
		{
			if (!m_file_open)
				return;

			SaveFileDialog dlg = new SaveFileDialog();
			dlg.FilterIndex = 1;

			/* PDF output types */
			if (type <= Save_Type_t.PDF)
			{
				dlg.Filter = "PDF Files(*.pdf)|*.pdf";
				if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					String options = null;
					bool use_gs = true;
					String init_file = CreatePDFXA(type);
					;
					switch (type)
					{
						case Save_Type_t.PDF:
							/* All done.  No need to use gs */
							System.IO.File.Copy(m_currfile, dlg.FileName, true);
							use_gs = false;
							break;
						case Save_Type_t.PDF13:
							options = "-dCompatibilityLevel=1.3";
							break;
						case Save_Type_t.PDFA1_CMYK:
							options = "-dPDFA=1 -dNOOUTERSAVE -dPDFACompatibilityPolicy=1 -sProcessColorModel=DeviceCMYK -dColorConversionStrategy=/CMYK -sOutputICCProfile=" + m_outputintents.cmyk_icc;
							break;
						case Save_Type_t.PDFA1_RGB:
							options = "-dPDFA=1 -dNOOUTERSAVE -dPDFACompatibilityPolicy=1 -sProcessColorModel=DeviceRGB -dColorConversionStrategy=/RGB -sOutputICCProfile=" + m_outputintents.rgb_icc;
							break;
						case Save_Type_t.PDFA2_CMYK:
							options = "-dPDFA=2 -dNOOUTERSAVE -dPDFACompatibilityPolicy=1 -sProcessColorModel=DeviceCMYK -dColorConversionStrategy=/CMYK -sOutputICCProfile=" + m_outputintents.cmyk_icc;
							break;
						case Save_Type_t.PDFA2_RGB:
							options = "-dPDFA=2 -dNOOUTERSAVE -dPDFACompatibilityPolicy=1 -sProcessColorModel=DeviceRGB -dColorConversionStrategy=/RGB -sOutputICCProfile=" + m_outputintents.rgb_icc;
							break;
						case Save_Type_t.PDFX3_CMYK:
							options = "-dPDFX -dNOOUTERSAVE -dPDFACompatibilityPolicy=1 -sProcessColorModel=DeviceCMYK -dColorConversionStrategy=/CMYK -sOutputICCProfile=" + m_outputintents.cmyk_icc;
							break;
						case Save_Type_t.PDFX3_GRAY:
							options = "-dPDFX -dNOOUTERSAVE -dPDFACompatibilityPolicy=1 -sProcessColorModel=DeviceGray -dColorConversionStrategy=/Gray -sOutputICCProfile=" + m_outputintents.cmyk_icc;
							break;

					}
					if (use_gs)
					{
						xaml_DistillProgress.Value = 0;
						if (m_ghostscript.Convert(m_currfile, options,
							Enum.GetName(typeof(gsDevice_t), gsDevice_t.pdfwrite),
							dlg.FileName, m_num_pages, 300, false, null, -1, -1,
							init_file, null) == gsStatus.GS_BUSY)
						{
							ShowMessage(NotifyType_t.MESS_STATUS, "GS busy");
							return;
						}
						xaml_DistillName.Text = "Creating PDF";
						xaml_CancelDistill.Visibility = System.Windows.Visibility.Collapsed;
						xaml_DistillName.FontWeight = FontWeights.Bold;
						xaml_DistillGrid.Visibility = System.Windows.Visibility.Visible;
					}
				}
			}
			else
			{
				/* Non PDF output */
				gsDevice_t Device = gsDevice_t.xpswrite;
				bool use_mupdf = true;
				switch (type)
				{
					case Save_Type_t.PCLBitmap:
						break;
					case Save_Type_t.PNG:
						break;
					case Save_Type_t.PWG:
						break;
					case Save_Type_t.SVG:
						break;
					case Save_Type_t.PCLXL:
						use_mupdf = false;
						dlg.Filter = "PCL-XL (*.bin)|*.bin";
						Device = gsDevice_t.pxlcolor;
						break;
					case Save_Type_t.TEXT:
						use_mupdf = false;
						dlg.Filter = "Text Files(*.txt)|*.txt";
						Device = gsDevice_t.txtwrite;
						break;
					case Save_Type_t.XPS:
						use_mupdf = false;
						dlg.Filter = "XPS Files(*.xps)|*.xps";
						break;
				}
				if (!use_mupdf)
				{
					if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					{
						if (m_ghostscript.Convert(m_currfile, "",
							Enum.GetName(typeof(gsDevice_t), Device),
							dlg.FileName, 1, 300, false, null, -1, -1,
							null, null) == gsStatus.GS_BUSY)
						{
							ShowMessage(NotifyType_t.MESS_STATUS, "GS busy");
							return;
						}
					}
				}
			}
		}

		private void SavePNG(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.PNG);
		}

		private void SavePWG(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.PWG);
		}

		private void SavePNM(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.PNM);
		}

		private void SaveSVG(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.SVG);
		}

		private void SavePCL(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.PCLBitmap);
		}

		private void SavePDF(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.PDF);
		}

		private void SaveText(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.TEXT);
		}

		private void SaveHTML(object sender, RoutedEventArgs e)
		{

		}

		private void SavePDF13(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.PDF13);
		}

		private void SavePDFX3_Gray(object sender, RoutedEventArgs e)
		{
			if (m_outputintents.gray_icc == null)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "Set Gray Output Intent ICC Profile");
				return;
			}
			SaveFile(Save_Type_t.PDFX3_GRAY);
		}

		private void SavePDFX3_CMYK(object sender, RoutedEventArgs e)
		{
			if (m_outputintents.cmyk_icc == null)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "Set CMYK Output Intent ICC Profile");
				return;
			}
			SaveFile(Save_Type_t.PDFX3_CMYK);
		}

		private void SavePDFA1_RGB(object sender, RoutedEventArgs e)
		{
			if (m_outputintents.rgb_icc == null)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "Set RGB Output Intent ICC Profile");
				return;
			}
			SaveFile(Save_Type_t.PDFA1_RGB);
		}

		private void SavePDFA1_CMYK(object sender, RoutedEventArgs e)
		{
			if (m_outputintents.cmyk_icc == null)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "Set CMYK Output Intent ICC Profile");
				return;
			}
			SaveFile(Save_Type_t.PDFA1_CMYK);
		}

		private void SavePDFA2_RGB(object sender, RoutedEventArgs e)
		{
			if (m_outputintents.rgb_icc == null)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "Set RGB Output Intent ICC Profile");
				return;
			}
			SaveFile(Save_Type_t.PDFA2_RGB);
		}

		private void SavePDFA2_CMYK(object sender, RoutedEventArgs e)
		{
			if (m_outputintents.cmyk_icc == null)
			{
				ShowMessage(NotifyType_t.MESS_STATUS, "Set CMYK Output Intent ICC Profile");
				return;
			}
			SaveFile(Save_Type_t.PDFA2_CMYK);
		}

		private void SavePCLXL(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.PCLXL);
		}
		private void SaveXPS(object sender, RoutedEventArgs e)
		{
			SaveFile(Save_Type_t.XPS);
		}
		#endregion SaveAs

		#region Extract
		private void Extract(Extract_Type_t type)
		{
			if (m_selection != null || !m_init_done)
				return;

			m_selection = new Selection(m_currpage + 1, m_doczoom, type);
			m_selection.UpdateMain += new Selection.CallBackMain(SelectionMade);
			m_selection.Show();
			m_selection.xaml_Image.Source = m_docPages[m_currpage].BitMap;
			m_selection.xaml_Image.Height = m_docPages[m_currpage].Height;
			m_selection.xaml_Image.Width = m_docPages[m_currpage].Width;
		}

		async private void SelectionZoom(int page_num, double zoom)
		{
			Point ras_size;
			if (ComputePageSize(page_num, zoom, out ras_size) == status_t.S_ISOK)
			{
				try
				{
					Byte[] bitmap = new byte[(int)ras_size.X * (int)ras_size.Y * 4];
					BlocksText charlist;

					Task<int> ren_task =
						new Task<int>(() => mu_doc.RenderPage(page_num, bitmap,
							(int)ras_size.X, (int)ras_size.Y, zoom, false, true,
							false, out charlist));
					ren_task.Start();
					await ren_task.ContinueWith((antecedent) =>
					{
						status_t code = (status_t)ren_task.Result;
						if (code == status_t.S_ISOK)
						{
							if (m_selection != null)
							{
								int stride = (int)ras_size.X * 4;
								m_selection.xaml_Image.Source = BitmapSource.Create((int)ras_size.X, (int)ras_size.Y, 72, 72, PixelFormats.Pbgra32, BitmapPalettes.Halftone256, bitmap, stride);
								m_selection.xaml_Image.Height = (int)ras_size.Y;
								m_selection.xaml_Image.Width = (int)ras_size.X;
								m_selection.UpdateRect();
								m_selection.m_curr_state = SelectStatus_t.OK;
							}
						}
					}, TaskScheduler.FromCurrentSynchronizationContext());
				}
				catch (OutOfMemoryException e)
				{
					Console.WriteLine("Memory allocation failed page " + page_num + "\n");
					ShowMessage(NotifyType_t.MESS_ERROR, "Out of memory: " + e.Message);
				}
			}
		}

		private void SelectionMade(object gsObject, SelectEventArgs results)
		{
			switch (results.State)
			{
				case SelectStatus_t.CANCEL:
				case SelectStatus_t.CLOSE:
					m_selection = null;
					return;
				case SelectStatus_t.SELECT:
					/* Get the information we need */
					double zoom = results.ZoomFactor;
					Point start = results.TopLeft;
					Point size = results.Size;
					int page = results.PageNum;
					gsDevice_t Device = gsDevice_t.pdfwrite;

					start.X = start.X / zoom;
					start.Y = start.Y / zoom;
					size.X = size.X / zoom;
					size.Y = size.Y / zoom;

					/* Do the actual extraction */
					String options;
					SaveFileDialog dlg = new SaveFileDialog();
					dlg.FilterIndex = 1;

					/* Get us set up to do a fixed size */
					options = "-dFirstPage=" + page + " -dLastPage=" + page +
						" -dDEVICEWIDTHPOINTS=" + size.X + " -dDEVICEHEIGHTPOINTS=" +
						size.Y + " -dFIXEDMEDIA";

					/* Set up the translation */
					String init_string = "<</Install {-" + start.X + " -" +
						start.Y + " translate (testing) == flush}>> setpagedevice";

					switch (results.Type)
					{
						case Extract_Type_t.PDF:
							dlg.Filter = "PDF Files(*.pdf)|*.pdf";
							break;
						case Extract_Type_t.EPS:
							dlg.Filter = "EPS Files(*.eps)|*.eps";
							Device = gsDevice_t.eps2write;
							break;
						case Extract_Type_t.PS:
							dlg.Filter = "PostScript Files(*.ps)|*.ps";
							Device = gsDevice_t.ps2write;
							break;
						case Extract_Type_t.SVG:
							dlg.Filter = "SVG Files(*.svg)|*.svg";
							break;
					}
					if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					{
						if (m_ghostscript.Convert(m_currfile, options,
							Enum.GetName(typeof(gsDevice_t), Device),
							dlg.FileName, 1, 300, false, null, page, page,
							null, init_string) == gsStatus.GS_BUSY)
						{
							ShowMessage(NotifyType_t.MESS_STATUS, "GS busy");
							return;
						}
					}
					m_selection.Close();
					break;
				case SelectStatus_t.ZOOMIN:
					/* Render new page at this resolution and hand it off */
					SelectionZoom(results.PageNum - 1, results.ZoomFactor);
					break;
				case SelectStatus_t.ZOOMOUT:
					/* Render new page at this resolution and hand it off */
					SelectionZoom(results.PageNum - 1, results.ZoomFactor);
					break;
			}
		}

		private void ExtractPDF(object sender, RoutedEventArgs e)
		{
			Extract(Extract_Type_t.PDF);
		}
		private void ExtractEPS(object sender, RoutedEventArgs e)
		{
			Extract(Extract_Type_t.EPS);
		}
		private void ExtractPS(object sender, RoutedEventArgs e)
		{
			Extract(Extract_Type_t.PS);
		}
		private void ExtractSVG(object sender, RoutedEventArgs e)
		{
			Extract(Extract_Type_t.SVG);
		}
		private void OutputIntents(object sender, RoutedEventArgs e)
		{
			m_outputintents.Show();
		}
		#endregion Extract

		#region Search
		/* Search related code */
		private void Search(object sender, RoutedEventArgs e)
		{
			if (!m_init_done || (m_textsearch != null && m_textsearch.IsBusy))
				return;

			m_textsearch = null; /* Start out fresh */
			if (xaml_SearchControl.Visibility == System.Windows.Visibility.Collapsed)
				xaml_SearchControl.Visibility = System.Windows.Visibility.Visible;
			else
			{
				xaml_SearchControl.Visibility = System.Windows.Visibility.Collapsed;
				xaml_SearchGrid.Visibility = System.Windows.Visibility.Collapsed;
				ClearTextSearch();
			}
		}

		private void OnSearchBackClick(object sender, RoutedEventArgs e)
		{
			String textToFind = xaml_SearchText.Text;
			TextSearchSetUp(-1, textToFind);
		}

		private void OnSearchForwardClick(object sender, RoutedEventArgs e)
		{
			String textToFind = xaml_SearchText.Text;
			TextSearchSetUp(1, textToFind);
		}

		/* The thread that is actually doing the search work */
		void SearchWork(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = sender as BackgroundWorker;
			List<object> genericlist = e.Argument as List<object>;
			int direction = (int)genericlist[0];
			String needle = (String)genericlist[1];
			/* To make sure we get the next page or current page during search */
			int in_search = (int)genericlist[2];
			m_searchpage = m_currpage + direction * in_search;
			searchResults_t results = new searchResults_t();

			/* Break if we find something, get to the end (or start of doc) 
			 * or if we have a cancel occur */
			while (true)
			{
				int box_count = mu_doc.TextSearchPage(m_searchpage, needle);
				int percent;

				if (direction == 1)
					percent = (int)(100.0 * ((double)m_searchpage + 1) / (double)m_num_pages);
				else
					percent = 100 - (int)(100.0 * ((double)m_searchpage) / (double)m_num_pages);

				if (box_count > 0)
				{
					/* This page has something lets go ahead and extract and 
					 * signal to the UI thread and end this thread */
					results.done = false;
					results.num_rects = box_count;
					results.page_found = m_searchpage;
					results.rectangles = new List<Rect>();

					for (int kk = 0; kk < box_count; kk++)
					{
						Point top_left;
						Size size;
						mu_doc.GetTextSearchItem(kk, out top_left, out size);
						var rect = new Rect(top_left, size);
						results.rectangles.Add(rect);
					}
					/* Reset global smart pointer once we have everything */
					mu_doc.ReleaseTextSearch();
					worker.ReportProgress(percent, results);
					break;
				}
				else
				{
					/* This page has nothing.  Lets go ahead and just update
					 * the progress bar */
					worker.ReportProgress(percent, null);
					if (percent >= 100)
					{
						results.done = true;
						results.needle = needle;
						break;
					}
					m_searchpage = m_searchpage + direction;
				}
				if (worker.CancellationPending == true)
				{
					e.Cancel = true;
					break;
				}
			}
			e.Result = results;
		}

		private void SearchProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			if (e.UserState == null)
			{
				/* Nothing found */
				xaml_SearchProgress.Value = e.ProgressPercentage;
			}
			else
			{
				m_text_list = new List<RectList>();
				/* found something go to page and show results */
				searchResults_t results = (searchResults_t)e.UserState;
				xaml_SearchProgress.Value = e.ProgressPercentage;
				m_currpage = results.page_found;
				/* Add in the rectangles */
				for (int kk = 0; kk < results.num_rects; kk++)
				{
					var rect_item = new RectList();
					rect_item.Scale = m_doczoom;
					rect_item.Color = m_textsearchcolor;
					rect_item.Height = results.rectangles[kk].Height * m_doczoom;
					rect_item.Width = results.rectangles[kk].Width * m_doczoom;
					rect_item.X = results.rectangles[kk].X * m_doczoom;
					rect_item.Y = results.rectangles[kk].Y * m_doczoom;
					rect_item.Index = kk.ToString();
					m_text_list.Add(rect_item);
				}
				m_docPages[results.page_found].TextBox = m_text_list;
				m_doscroll = true;
				xaml_PageList.ScrollIntoView(m_docPages[results.page_found]);
			}
		}

		private void SearchCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (e.Cancelled == true)
			{
				xaml_SearchGrid.Visibility = System.Windows.Visibility.Collapsed;
				m_textsearch = null;
			}
			else
			{
				searchResults_t results = (searchResults_t)e.Result;
				if (results.done == true)
				{
					xaml_SearchGrid.Visibility = System.Windows.Visibility.Collapsed;
					m_textsearch = null;
					ShowMessage(NotifyType_t.MESS_STATUS, "End of document search for \"" + results.needle + "\"");
				}
			}
		}

		private void CancelSearchClick(object sender, RoutedEventArgs e)
		{
			if (m_textsearch != null && m_textsearch.IsBusy)
				m_textsearch.CancelAsync();
			xaml_SearchGrid.Visibility = System.Windows.Visibility.Collapsed;
			m_textsearch = null;
			ClearTextSearch();
		}

		private void ClearTextSearch()
		{
			for (int kk = 0; kk < m_num_pages; kk++)
			{
				var temp = m_docPages[kk].TextBox;
				if (temp != null)
				{
					m_docPages[kk].TextBox = null;
				}
			}
		}

		private void ScaleTextBox(int pagenum)
		{
			var temp = m_docPages[pagenum].TextBox;
			for (int kk = 0; kk < temp.Count; kk++)
			{
				var rect_item = temp[kk];
				double factor = m_doczoom / temp[kk].Scale;

				temp[kk].Height = temp[kk].Height * factor;
				temp[kk].Width = temp[kk].Width * factor;
				temp[kk].X = temp[kk].X * factor;
				temp[kk].Y = temp[kk].Y * factor;

				temp[kk].Scale = m_doczoom;
				temp[kk].PageRefresh();
			}
			m_docPages[pagenum].TextBox = temp;
		}

		private void TextSearchSetUp(int direction, String needle)
		{
			/* Create background task for performing text search. */
			try
			{
				int in_text_search = 0;

				if (m_textsearch != null && m_textsearch.IsBusy)
					return;

				if (m_textsearch != null)
				{
					in_text_search = 1;
					m_textsearch = null;
				}
				if (m_prevsearch != null && needle != m_prevsearch)
				{
					in_text_search = 0;
					ClearTextSearch();
				}

				if (m_textsearch == null)
				{
					m_prevsearch = needle;
					m_textsearch = new BackgroundWorker();
					m_textsearch.WorkerReportsProgress = true;
					m_textsearch.WorkerSupportsCancellation = true;
					var arguments = new List<object>();
					arguments.Add(direction);
					arguments.Add(needle);
					arguments.Add(in_text_search);
					m_textsearch.DoWork += new DoWorkEventHandler(SearchWork);
					m_textsearch.RunWorkerCompleted += new RunWorkerCompletedEventHandler(SearchCompleted);
					m_textsearch.ProgressChanged += new ProgressChangedEventHandler(SearchProgressChanged);
					xaml_SearchGrid.Visibility = System.Windows.Visibility.Visible;
					m_textsearch.RunWorkerAsync(arguments);
				}
			}
			catch (OutOfMemoryException e)
			{
				Console.WriteLine("Memory allocation failed during text search\n");
				ShowMessage(NotifyType_t.MESS_ERROR, "Out of memory: " + e.Message);
			}
		}
		#endregion Search

		#region Link
		private void LinksToggle(object sender, RoutedEventArgs e)
		{
			if (!m_init_done)
				return;

			m_links_on = !m_links_on;

			if (m_page_link_list == null)
			{
				if (m_linksearch != null && m_linksearch.IsBusy)
					return;

				m_page_link_list = new List<List<RectList>>();
				m_linksearch = new BackgroundWorker();
				m_linksearch.WorkerReportsProgress = false;
				m_linksearch.WorkerSupportsCancellation = true;
				m_linksearch.DoWork += new DoWorkEventHandler(LinkWork);
				m_linksearch.RunWorkerCompleted += new RunWorkerCompletedEventHandler(LinkCompleted);
				m_linksearch.RunWorkerAsync();
			}
			else
			{
				if (m_links_on)
					LinksOn();
				else
					LinksOff();
			}
		}

		private void LinkWork(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = sender as BackgroundWorker;

			for (int k = 0; k < m_num_pages; k++)
			{
				int box_count = mu_doc.GetLinksPage(k);
				List<RectList> links = new List<RectList>();
				if (box_count > 0)
				{
					var rectlist = new RectList();
					for (int j = 0; j < box_count; j++)
					{
						Point top_left;
						Size size;
						String uri;
						int type;
						int topage;

						mu_doc.GetLinkItem(j, out top_left, out size, out uri,
							out topage, out type);
						rectlist.Height = size.Height * m_doczoom;
						rectlist.Width = size.Width * m_doczoom;
						rectlist.X = top_left.X * m_doczoom;
						rectlist.Y = top_left.Y * m_doczoom;
						rectlist.Color = m_linkcolor;
						rectlist.Index = k.ToString() + "." + j.ToString();
						rectlist.PageNum = topage;
						rectlist.Scale = m_doczoom;
						if (uri != null)
							rectlist.Urilink = new Uri(uri);
						rectlist.Type = (Link_t)type;
						links.Add(rectlist);
					}
				}
				mu_doc.ReleaseLink();
				m_page_link_list.Add(links);

				if (worker.CancellationPending == true)
				{
					e.Cancel = true;
					break;
				}
			}
		}

		private void LinkCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			LinksOn();
		}

		private void ScaleLinkBox(int pagenum)
		{
			var temp = m_docPages[pagenum].LinkBox;
			for (int kk = 0; kk < temp.Count; kk++)
			{
				var rect_item = temp[kk];
				double factor = m_doczoom / temp[kk].Scale;

				temp[kk].Height = temp[kk].Height * factor;
				temp[kk].Width = temp[kk].Width * factor;
				temp[kk].X = temp[kk].X * factor;
				temp[kk].Y = temp[kk].Y * factor;

				temp[kk].Scale = m_doczoom;
				temp[kk].PageRefresh();
			}
			m_docPages[pagenum].LinkBox = temp;
		}
		/* Merge these */
		private void ScaleTextLines(int pagenum, double scale_factor)
		{
			var temp = m_lineptrs[pagenum];
			for (int kk = 0; kk < temp.Count; kk++)
			{
				var rect_item = temp[kk];
				double factor = scale_factor / temp[kk].Scale;

				temp[kk].Height = temp[kk].Height * factor;
				temp[kk].Width = temp[kk].Width * factor;
				temp[kk].X = temp[kk].X * factor;
				temp[kk].Y = temp[kk].Y * factor;

				temp[kk].Scale = scale_factor;
			}
			m_lineptrs[pagenum] = temp;
		}

		private void ScaleTextBlocks(int pagenum, double scale_factor)
		{
			var temp = m_textptrs[pagenum];
			for (int kk = 0; kk < temp.Count; kk++)
			{
				var rect_item = temp[kk];
				double factor = scale_factor / temp[kk].Scale;

				temp[kk].Height = temp[kk].Height * factor;
				temp[kk].Width = temp[kk].Width * factor;
				temp[kk].X = temp[kk].X * factor;
				temp[kk].Y = temp[kk].Y * factor;

				temp[kk].Scale = scale_factor;
			}
			m_textptrs[pagenum] = temp;
		}

		/* Only visible pages */
		private void LinksOff()
		{
			int range = (int)Math.Ceiling(((double)m_numpagesvisible - 1.0) / 2.0);

			for (int kk = m_currpage - range; kk <= m_currpage + range; kk++)
			{
				var temp = m_docPages[kk].LinkBox;
				if (temp != null)
				{
					m_docPages[kk].LinkBox = null;
				}
			}
		}

		/* Only visible pages */
		private void LinksOn()
		{
			int range = (int)Math.Ceiling(((double)m_numpagesvisible - 1.0) / 2.0);

			for (int kk = m_currpage - range; kk <= m_currpage + range; kk++)
			{
				if (!(kk < 0 || kk > m_num_pages - 1))
				{
					var temp = m_docPages[kk].LinkBox;
					if (temp == null)
					{
						m_docPages[kk].LinkBox = m_page_link_list[kk];
					}
				}
			}
		}

		private void LinkClick(object sender, MouseButtonEventArgs e)
		{
			var item = (Rectangle)sender;

			if (item == null)
				return;

			String tag = (String)item.Tag;
			int page = 0;
			int index = 0;

			if (tag == null || tag.Length < 3 || !(tag.Contains('.')))
				return;

			String[] parts = tag.Split('.');
			try
			{
				page = System.Convert.ToInt32(parts[0]);
				index = System.Convert.ToInt32(parts[1]);

			}
			catch (FormatException e1)
			{
				Console.WriteLine("String is not a sequence of digits.");
			}
			catch (OverflowException e2)
			{
				Console.WriteLine("The number cannot fit in an Int32.");
			}

			if (index >= 0 && index < m_num_pages && page >= 0 && page < m_num_pages)
			{
				var link_list = m_page_link_list[page];
				var link = link_list[index];

				if (link.Type == Link_t.LINK_GOTO)
				{
					if (m_currpage != link.PageNum && link.PageNum >= 0 &&
						link.PageNum < m_num_pages)
						RenderRange(link.PageNum, true);
				}
				else if (link.Type == Link_t.LINK_URI)
					System.Diagnostics.Process.Start(link.Urilink.AbsoluteUri);
			}
		}
		#endregion Link

		#region TextSelection

		/* Change cursor if we are over text block */
		private void ExitTextBlock(object sender, System.Windows.Input.MouseEventArgs e)
		{
			this.Cursor = System.Windows.Input.Cursors.Arrow;
		}

		private void EnterTextBlock(object sender, System.Windows.Input.MouseEventArgs e)
		{
			this.Cursor = System.Windows.Input.Cursors.IBeam;
		}

		private void ClearSelections()
		{
			for (int kk = 0; kk < m_textSelect.Count; kk++)
			{
				m_lineptrs[m_textSelect[kk].pagenum].Clear();
				if (m_docPages[m_textSelect[kk].pagenum].SelectedLines != null)
					m_docPages[m_textSelect[kk].pagenum].SelectedLines.Clear();
			}
			m_textSelect.Clear();
			m_textselected = false;
		}

		private void InitTextSelection(DocPage page)
		{
			if (m_textSelect != null)
				ClearSelections();
			else
				m_textSelect = new List<textSelectInfo_t>();

			m_intxtselect = true;

			textSelectInfo_t selinfo = new textSelectInfo_t();
			selinfo.pagenum = page.PageNum;
			selinfo.first_line_full = false;
			selinfo.last_line_full = false;
			m_textSelect.Add(selinfo);
		}

		private void PageMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (this.Cursor != System.Windows.Input.Cursors.IBeam)
				return;

			var page = ((FrameworkElement)e.Source).DataContext as DocPage;
			Canvas can = ((FrameworkElement)e.Source).Parent as Canvas;
			if (page == null || can == null)
				return;

			InitTextSelection(page);
			var posit = e.GetPosition(can);

			page.SelX = posit.X;
			page.SelY = posit.Y;
			page.SelAnchorX = posit.X;
			page.SelAnchorY = posit.Y;
			page.SelColor = m_regionselect;

			/* Create new holder for lines highlighted */
			m_lineptrs[page.PageNum] = new LinesText();
		}

		private void PageMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Released || m_intxtselect == false)
				return;

			var page = ((FrameworkElement)e.Source).DataContext as DocPage;
			Canvas can = ((FrameworkElement)e.Source).Parent as Canvas;
			if (page == null || can == null)
				return;
			if (page.PageNum < 0)
				return;
			/* Store the location of our most recent page in case we exit window */
			var pos = e.GetPosition(can);
			m_lastY = pos.Y;
			m_maxY = can.Height;
			/* Don't allow the listview to maintain control of the mouse, we need
			 * to detect if we leave the window */
			/* Make sure page is rendered */
			if (page.Content != Page_Content_t.FULL_RESOLUTION ||
				page.Zoom != m_doczoom)
			{
				RenderRange(page.PageNum, false);
			}

			UpdateSelection(pos, page);
		}

		/* Resize selection rect */
		private void UpdateSelection(System.Windows.Point pos, DocPage page)
		{
			bool new_page = true;
			TextLine start_line, end_line;
			double x = 0, y, w = 0, h;
			bool found_first = false;
			bool above_anchor = true;
			bool first_line_full = false;
			bool last_line_full = false;

			for (int kk = 0; kk < m_textSelect.Count; kk++)
				if (m_textSelect[kk].pagenum == page.PageNum)
					new_page = false;

			/* See if we have gone back to a previous page */
			if (!new_page && page.PageNum != m_textSelect[m_textSelect.Count - 1].pagenum)
			{
				DocPage curr_page = m_docPages[m_textSelect[m_textSelect.Count - 1].pagenum];
				curr_page.SelHeight = 0;
				curr_page.SelWidth = 0;
				m_textSelect.RemoveAt(m_textSelect.Count - 1);
				m_lineptrs[curr_page.PageNum].Clear();
				curr_page.SelectedLines.Clear();
			}
			if (new_page)
			{
				/* New page */
				page.SelX = pos.X;
				page.SelY = pos.Y;
				page.SelAnchorX = m_docPages[m_textSelect[m_textSelect.Count - 1].pagenum].SelAnchorX;
				if (m_textSelect[m_textSelect.Count - 1].pagenum > page.PageNum)
				{
					page.SelAnchorY = page.Height;
				}
				else
				{
					page.SelAnchorY = 0;
				}
				page.SelColor = m_regionselect;
				textSelectInfo_t info = new textSelectInfo_t();
				info.pagenum = page.PageNum;
				info.first_line_full = false;
				info.last_line_full = false;
				m_textSelect.Add(info);
				/* Create new holder for lines highlighted */
				m_lineptrs[page.PageNum] = new LinesText();
			}

			if (page.TextBlocks == null || page.TextBlocks.Count == 0)
				return;

			/* Width changes translate across the pages */
			for (int jj = 0; jj < m_textSelect.Count; jj++)
			{
				DocPage curr_page = m_docPages[m_textSelect[jj].pagenum];
				x = Math.Min(pos.X, curr_page.SelAnchorX);
				w = Math.Max(pos.X, curr_page.SelAnchorX) - x;
				curr_page.SelX = x;
				curr_page.SelWidth = w;
			}
			/* Height is just the current page */
			y = Math.Min(pos.Y, page.SelAnchorY);
			h = Math.Max(pos.Y, page.SelAnchorY) - y;

			/* Determine if we are going up or down */
			if (pos.Y > page.SelAnchorY)
				above_anchor = false;
			page.SelY = y;
			page.SelHeight = h;

			/* Clear out what we currently have */
			m_lineptrs[page.PageNum].Clear();

			/* Stuff already selected above us */
			if (m_textSelect.Count > 1)
				found_first = true;
			/* Moving backwards through pages */
			if (m_textSelect.Count > 1 && m_textSelect[m_textSelect.Count - 2].pagenum > page.PageNum)
				found_first = false;

			for (int jj = 0; jj < page.TextBlocks.Count; jj++)
			{
				/* Text blocks are already scaled. Lines are not */
				var intersect_blk = page.TextBlocks[jj].CheckIntersection(x, y, w, h);
				var lines = page.TextBlocks[jj].TextLines;

				if (intersect_blk == Intersection_t.FULL)
				{
					/* Just add all the lines for this block */
					for (int kk = 0; kk < lines.Count; kk++)
						m_lineptrs[page.PageNum].Add(lines[kk]);
					if (jj == 0)
					{
						first_line_full = true;
						found_first = true;
					}
					if (jj == page.TextBlocks.Count - 1)
						last_line_full = true;
				}
				else if (intersect_blk != Intersection_t.NONE)
				{
					/* Now go through the lines */
					for (int kk = 0; kk < lines.Count; kk++)
					{
						double scale = m_doczoom / lines[kk].Scale;
						var intersect_line = lines[kk].CheckIntersection(x * scale, y * scale, w * scale, h * scale);
						if (intersect_line == Intersection_t.FULL)
						{
							m_lineptrs[page.PageNum].Add(lines[kk]);
							found_first = true;
							if (jj == 0 && kk == 0)
								first_line_full = true;
							if (jj == page.TextBlocks.Count - 1 && 
								kk == lines.Count - 1)
								last_line_full = true;

						}
						else if (intersect_line == Intersection_t.PARTIAL)
						{
							double val;
							var lett = lines[kk].TextCharacters;

							/* Now go through the width. */
							if (found_first)
							{
								if (above_anchor)
									val = page.SelAnchorX;
								else
									val = pos.X;

								/* our second partial line */
								if (val > lines[kk].X * scale + lines[kk].Width * scale)
									m_lineptrs[page.PageNum].Add(lines[kk]);
								else
								{
									/* Use either anchor point or mouse pos */
									end_line = new TextLine();
									end_line.TextCharacters = new List<TextCharacter>();
									end_line.Height = 0;
									end_line.Scale = m_doczoom;
									for (int mm = 0; mm < lett.Count; mm++)
									{
										double letscale = m_doczoom / lett[mm].Scale;
										if (lett[mm].X * letscale < val)
										{
											/* Can set to special color for debug */
											end_line.Color = m_textselectcolor;
											/* special color for debug */
											//end_line.Color = "#4000FF00";
											end_line.Height = lines[kk].Height * scale;
											end_line.Width = lett[mm].X * letscale + lett[mm].Width * letscale - lines[kk].X * scale;
											end_line.Y = lines[kk].Y * scale;
											end_line.X = lines[kk].X * scale;
											end_line.TextCharacters.Add(lett[mm]);
										}
										else
											break;
									}
									if (end_line.Height != 0)
										m_lineptrs[page.PageNum].Add(end_line);
								}
							}
							else
							{
								if (!above_anchor)
									val = page.SelAnchorX;
								else
									val = pos.X;

								/* our first partial line */
								found_first = true;
								if (val < lines[kk].X * scale)
									m_lineptrs[page.PageNum].Add(lines[kk]);
								else
								{
									start_line = new TextLine();
									start_line.TextCharacters = new List<TextCharacter>();
									start_line.Height = 0;
									start_line.Scale = m_doczoom;
									/* Use either anchor point or mouse pos */
									for (int mm = 0; mm < lett.Count; mm++)
									{
										double letscale = m_doczoom / lett[mm].Scale;
										if (lett[mm].X * letscale + lett[mm].Width * letscale >= val)
										{
											start_line.Color = m_textselectcolor;
											/* special color for debug */
											//start_line.Color = "#40FF0000";
											start_line.Height = lines[kk].Height * scale;
											start_line.Width = lines[kk].X * scale + lines[kk].Width * scale - lett[mm].X * letscale;
											start_line.X = lett[mm].X * letscale;
											start_line.Y = lines[kk].Y * scale;
											start_line.TextCharacters.Add(lett[mm]);
											break;
										}
									}
									if (start_line.Height > 0)
										m_lineptrs[page.PageNum].Add(start_line);
								}
							}
						}
					}
				}
			}
			var txtsel = m_textSelect[m_textSelect.Count - 1];
			txtsel.first_line_full = first_line_full;
			txtsel.last_line_full = last_line_full;
			m_textSelect[m_textSelect.Count - 1] = txtsel;

			/* Adjust for scale before assigning */
			var temp = m_lineptrs[page.PageNum];
			for (int kk = 0; kk < temp.Count; kk++)
			{
				var rect_item = temp[kk];
				double factor = m_doczoom / rect_item.Scale;

				temp[kk].Height = temp[kk].Height * factor;
				temp[kk].Width = temp[kk].Width * factor;
				temp[kk].X = temp[kk].X * factor;
				temp[kk].Y = temp[kk].Y * factor;

				temp[kk].Scale = m_doczoom;
			}
			page.SelectedLines = m_lineptrs[page.PageNum];
		}

		/* A fix for handling column cases TODO FIXME */
		private void UpdateSelectionCol(System.Windows.Point pos, DocPage page)
		{
			bool new_page = true;
			TextLine start_line, end_line;
			double x = 0, y, w = 0, h;
			bool found_first = false;
			bool above_anchor = true;
			bool first_line_full = false;
			bool last_line_full = false;

			for (int kk = 0; kk < m_textSelect.Count; kk++)
				if (m_textSelect[kk].pagenum == page.PageNum)
					new_page = false;

			/* See if we have gone back to a previous page */
			if (!new_page && page.PageNum != m_textSelect[m_textSelect.Count - 1].pagenum)
			{
				DocPage curr_page = m_docPages[m_textSelect[m_textSelect.Count - 1].pagenum];
				curr_page.SelHeight = 0;
				curr_page.SelWidth = 0;
				m_textSelect.RemoveAt(m_textSelect.Count - 1);
				m_lineptrs[curr_page.PageNum].Clear();
				curr_page.SelectedLines.Clear();
			}
			if (new_page)
			{
				/* New page */
				page.SelX = pos.X;
				page.SelY = pos.Y;
				page.SelAnchorX = m_docPages[m_textSelect[m_textSelect.Count - 1].pagenum].SelAnchorX;
				if (m_textSelect[m_textSelect.Count - 1].pagenum > page.PageNum)
				{
					page.SelAnchorY = page.Height;
				}
				else
				{
					page.SelAnchorY = 0;
				}
				page.SelColor = m_regionselect;
				textSelectInfo_t info = new textSelectInfo_t();
				info.pagenum = page.PageNum;
				info.first_line_full = false;
				info.last_line_full = false;
				m_textSelect.Add(info);
				/* Create new holder for lines highlighted */
				m_lineptrs[page.PageNum] = new LinesText();
			}

			if (page.TextBlocks == null || page.TextBlocks.Count == 0)
				return;

			/* Width changes translate across the pages */
			for (int jj = 0; jj < m_textSelect.Count; jj++)
			{
				DocPage curr_page = m_docPages[m_textSelect[jj].pagenum];
				x = Math.Min(pos.X, curr_page.SelAnchorX);
				w = Math.Max(pos.X, curr_page.SelAnchorX) - x;
				curr_page.SelX = x;
				curr_page.SelWidth = w;
			}
			/* Height is just the current page */
			y = Math.Min(pos.Y, page.SelAnchorY);
			h = Math.Max(pos.Y, page.SelAnchorY) - y;

			/* Determine if we are going up or down */
			if (pos.Y > page.SelAnchorY)
				above_anchor = false;
			page.SelY = y;
			page.SelHeight = h;

			/* Clear out what we currently have */
			m_lineptrs[page.PageNum].Clear();

			/* Stuff already selected above us */
			if (m_textSelect.Count > 1)
				found_first = true;
			/* Moving backwards through pages */
			if (m_textSelect.Count > 1 && m_textSelect[m_textSelect.Count - 2].pagenum > page.PageNum)
				found_first = false;

			/* To properly handle the multiple columns we have to find the last 
			 * line and make sure that all blocks between our first and last
			 * line are included. To do this we do an initial step through the
			 * blocks looking at our intersections */
			int first_block = -1;
			int last_block = -1;
			for (int jj = 0; jj < page.TextBlocks.Count; jj++ )
			{
				var intersect_blk = page.TextBlocks[jj].CheckIntersection(x, y, w, h);
				if (intersect_blk == Intersection_t.NONE && first_block != -1)
				{
					last_block = jj; /* NB: this is just past last block */
					break;
				}
				else if (intersect_blk != Intersection_t.NONE && first_block == -1)
					first_block = jj; /* NB: this is the first block */
			}
			if (first_block == -1)
				return;
			if (last_block == -1)
			{
				/* Only 1 block */
				last_block = first_block + 1;
			}

			for (int jj = first_block; jj < last_block; jj++)
			{
				/* Text blocks are already scaled. Lines are not */
				var intersect_blk = page.TextBlocks[jj].CheckIntersection(x, y, w, h);
				var lines = page.TextBlocks[jj].TextLines;

				if (jj == first_block || jj == last_block - 1)
				{
					/* Partial cases */
					if (intersect_blk == Intersection_t.FULL)
					{
						for (int kk = 0; kk < lines.Count; kk++)
							m_lineptrs[page.PageNum].Add(lines[kk]);
						if (jj == first_block)
						{
							first_line_full = true;
							found_first = true;
						}
						if (jj == last_block - 1)
						{
							last_line_full = true;
						}
					}
					else if (intersect_blk == Intersection_t.PARTIAL)
					{
						for (int kk = 0; kk < lines.Count; kk++)
						{
							double scale = m_doczoom / lines[kk].Scale;
							var intersect_line = lines[kk].CheckIntersection(x * scale, y * scale, w * scale, h * scale);
							if (intersect_line == Intersection_t.FULL)
							{
								m_lineptrs[page.PageNum].Add(lines[kk]);
								found_first = true;
								if (jj == 0 && kk == 0)
									first_line_full = true;
								if (jj == page.TextBlocks.Count - 1 &&
									kk == lines.Count - 1)
									last_line_full = true;

							}
							else if (intersect_line == Intersection_t.PARTIAL)
							{
								double val;
								var lett = lines[kk].TextCharacters;

								/* Now go through the width. */
								if (found_first)
								{
									if (above_anchor)
										val = page.SelAnchorX;
									else
										val = pos.X;

									/* our second partial line */
									if (val > lines[kk].X * scale + lines[kk].Width * scale)
										m_lineptrs[page.PageNum].Add(lines[kk]);
									else
									{
										/* Use either anchor point or mouse pos */
										end_line = new TextLine();
										end_line.TextCharacters = new List<TextCharacter>();
										end_line.Height = 0;
										end_line.Scale = m_doczoom;
										for (int mm = 0; mm < lett.Count; mm++)
										{
											double letscale = m_doczoom / lett[mm].Scale;
											if (lett[mm].X * letscale < val)
											{
												/* Can set to special color for debug */
												end_line.Color = m_textselectcolor;
												/* special color for debug */
												//end_line.Color = "#4000FF00";
												end_line.Height = lines[kk].Height * scale;
												end_line.Width = lett[mm].X * letscale + lett[mm].Width * letscale - lines[kk].X * scale;
												end_line.Y = lines[kk].Y * scale;
												end_line.X = lines[kk].X * scale;
												end_line.TextCharacters.Add(lett[mm]);
											}
											else
												break;
										}
										if (end_line.Height != 0)
											m_lineptrs[page.PageNum].Add(end_line);
									}
								}
								else
								{
									if (!above_anchor)
										val = page.SelAnchorX;
									else
										val = pos.X;

									/* our first partial line */
									found_first = true;
									if (val < lines[kk].X * scale)
										m_lineptrs[page.PageNum].Add(lines[kk]);
									else
									{
										start_line = new TextLine();
										start_line.TextCharacters = new List<TextCharacter>();
										start_line.Height = 0;
										start_line.Scale = m_doczoom;
										/* Use either anchor point or mouse pos */
										for (int mm = 0; mm < lett.Count; mm++)
										{
											double letscale = m_doczoom / lett[mm].Scale;
											if (lett[mm].X * letscale + lett[mm].Width * letscale >= val)
											{
												start_line.Color = m_textselectcolor;
												/* special color for debug */
												//start_line.Color = "#40FF0000";
												start_line.Height = lines[kk].Height * scale;
												start_line.Width = lines[kk].X * scale + lines[kk].Width * scale - lett[mm].X * letscale;
												start_line.X = lett[mm].X * letscale;
												start_line.Y = lines[kk].Y * scale;
												start_line.TextCharacters.Add(lett[mm]);
												break;
											}
										}
										if (start_line.Height > 0)
											m_lineptrs[page.PageNum].Add(start_line);
									}
								}
							}
						}
					}
				}
				else
				{
					/* Add all the lines for the blocks between the first and last */
					for (int kk = 0; kk < lines.Count; kk++)
						m_lineptrs[page.PageNum].Add(lines[kk]);
				}
			}

			var txtsel = m_textSelect[m_textSelect.Count - 1];
			txtsel.first_line_full = first_line_full;
			txtsel.last_line_full = last_line_full;
			m_textSelect[m_textSelect.Count - 1] = txtsel;

			/* Adjust for scale before assigning */
			var temp = m_lineptrs[page.PageNum];
			for (int kk = 0; kk < temp.Count; kk++)
			{
				var rect_item = temp[kk];
				double factor = m_doczoom / rect_item.Scale;

				temp[kk].Height = temp[kk].Height * factor;
				temp[kk].Width = temp[kk].Width * factor;
				temp[kk].X = temp[kk].X * factor;
				temp[kk].Y = temp[kk].Y * factor;

				temp[kk].Scale = m_doczoom;
			}
			page.SelectedLines = m_lineptrs[page.PageNum];
		}

		private void CheckIfSelected()
		{
			m_textselected = false;
			/* Check if anything was selected */
			for (int kk = 0; kk < m_lineptrs.Count; kk++)
			{
				if (m_lineptrs[kk].Count > 0)
				{
					m_textselected = true;
					break;
				}
			}
		}

		/* Rect should be removed */
		private void PageLeftClickUp(object sender, MouseButtonEventArgs e)
		{
			m_intxtselect = false;
			CheckIfSelected();
		}

		private void StepScroll(int stepsize)
		{
			ScrollViewer viewer = FindScrollViewer(xaml_PageList);
			if (viewer != null)
			{
				var scrollpos = viewer.VerticalOffset;
				viewer.ScrollToVerticalOffset(scrollpos + stepsize);
			}
		}

		/* Recursive call to find the scroll viewer */
		private ScrollViewer FindScrollViewer(DependencyObject d)
		{
			if (d is ScrollViewer)
				return d as ScrollViewer;

			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
			{
				var sw = FindScrollViewer(VisualTreeHelper.GetChild(d, i));
				if (sw != null) return sw;
			}
			return null;
		}

		/* Only worry about cases where we are moving and left button is down */
		private void ListPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			var relPoint = e.GetPosition(xaml_PageList);
			var absPoint = this.PointToScreen(relPoint);
			Console.Write("abs Y position = " + absPoint.Y + "\n");
			Console.Write("rel Y position = " + relPoint.Y + "\n");
			Console.Write("Height is = " + (this.Top + this.Height) + "\n");

			if (xaml_PageList.IsMouseCaptured == true)
			{
				if (!m_intxtselect)
				{
					xaml_PageList.ReleaseMouseCapture();
					e.Handled = true;
					return;
				}

				if (relPoint.Y < Constants.SCROLL_EDGE_BUFFER ||
					absPoint.Y > (this.Top + this.Height - Constants.SCROLL_EDGE_BUFFER))
				{
					if (m_dispatcherTimer == null)
					{
						m_dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
						m_dispatcherTimer.Tick += new EventHandler(dispatcherTimerTick);
						m_dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, Constants.DISPATCH_TIME);
					}
					if (m_dispatcherTimer.IsEnabled == false)
						m_dispatcherTimer.Start();
					e.Handled = true;
				}

				/* This is not desirable, but the scrollviewer behaves badly
				 * when it has captured the mouse and we move beyond the
				 * range. So we wont allow it */
				if (relPoint.Y < 0 ||
					absPoint.Y > (this.Top + this.Height) - Constants.SCROLL_EDGE_BUFFER / 2.0)
				{
					xaml_PageList.ReleaseMouseCapture();
					e.Handled = true;
					if (m_dispatcherTimer != null && m_dispatcherTimer.IsEnabled == true)
						m_dispatcherTimer.Stop();
					return;
				}
			}
		}

		private void ListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (m_dispatcherTimer != null && m_dispatcherTimer.IsEnabled)
			{
				m_dispatcherTimer.Stop();
			}
		}

		private void ListMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (m_dispatcherTimer != null && m_dispatcherTimer.IsEnabled)
			{
				m_dispatcherTimer.Stop();
			}
			if (xaml_PageList.IsMouseCaptured == true)
				xaml_PageList.ReleaseMouseCapture();
		}

		/* Get mouse position, update selection accordingly */
		private void dispatcherTimerTick(object sender, EventArgs e)
		{
			var position = this.PointToScreen(Mouse.GetPosition(xaml_PageList));
			Console.Write("Y position = " + position.Y + "\n");
			Console.Write("Top position = " + this.Top + "\n");
			Console.Write("Bottom position = " + (this.Top + this.Height) + "\n");

			DocPage page;
			int page_num;

			if (!xaml_PageList.IsMouseCaptured)
			{
				Console.Write("Lost capture\n");
				return;
			}
			else
			{
				Console.Write("Have capture\n");
			}
			/* Get our most recent page */
			var pageinfo = m_textSelect[m_textSelect.Count - 1];
			page_num = pageinfo.pagenum;

			/* Scrolling up */
			if (position.Y > this.Top + this.Height - Constants.SCROLL_EDGE_BUFFER)
			{
				/* See if we have the last line for this page */
				if (pageinfo.last_line_full)
				{
					page_num = page_num + 1;
					m_lastY = 0;
					if (page_num >= m_num_pages)
						return;
				}
				page = m_docPages[page_num];
				StepScroll(Constants.SCROLL_STEP);
				/* Set position for proper selection update */
				m_lastY = m_lastY + Constants.SCROLL_STEP;
				if (m_lastY > m_maxY)
					m_lastY = m_maxY;
				position.Y = m_lastY;
				UpdateSelection(position, page);
			}
			else if (position.Y < this.Top + Constants.SCROLL_EDGE_BUFFER)
			{
				/* See if we have the first line for this page */
				if (pageinfo.first_line_full)
				{
					if (page_num <= 0)
						return;
					page_num = page_num - 1;
					m_lastY = m_docPages[page_num].Height;
				}
				page = m_docPages[page_num];
				StepScroll(-Constants.SCROLL_STEP);
				/* Set position for proper selection update */
				m_lastY = m_lastY - Constants.SCROLL_STEP;
				if (m_lastY < 0)
					m_lastY = 0;
				position.Y = m_lastY;
				UpdateSelection(position, page);
			}
		}

		private void ListPreviewLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (m_dispatcherTimer != null && m_dispatcherTimer.IsEnabled)
			{
				m_dispatcherTimer.Stop();
			}
		}

		private void ShowContextMenu(object sender, MouseButtonEventArgs e)
		{
			if (this.Cursor != System.Windows.Input.Cursors.IBeam)
				return;

			var contextmenu = new System.Windows.Controls.ContextMenu();
			Canvas can = ((FrameworkElement)e.Source).Parent as Canvas;
			var page = ((FrameworkElement)e.Source).DataContext as DocPage;
			if (can == null || page == null)
				return;

			var posit = e.GetPosition(can);
			ContextMenu_t info = new ContextMenu_t();
			info.mouse_position = posit;
			info.page_num = page.PageNum;
			can.ContextMenu = contextmenu;

			if (m_textselected)
			{
				var m1 = new System.Windows.Controls.MenuItem();
				m1.Header = "Copy";

				/* amazing what I have to do here to get the icon out of the
				 * resources into something that wpf can use */
				var iconres = Properties.Resources.copy;
				var bitmap = iconres.ToBitmap();
				using (MemoryStream memory = new MemoryStream())
				{
					bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
					memory.Position = 0;
					BitmapImage bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.StreamSource = memory;
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.EndInit();
					Image iconImage = new Image();
					iconImage.Source = bitmapImage;
					m1.Icon = iconImage;
					m1.Click += cntxMenuCopy;
					contextmenu.Items.Add(m1);
				}

				var m6 = new System.Windows.Controls.MenuItem();
				m6.Header = "Deselect All";
				m6.Click += cntxMenuDeselectAll;
				contextmenu.Items.Add(m6);

				/* Below to be enabled when we add annotations */
				/*
				var ma1 = new System.Windows.Controls.MenuItem();
				ma1.Header = "Highlight";
				ma1.Click += cntxMenuHighlight;
				contextmenu.Items.Add(ma1);

				var ma2 = new System.Windows.Controls.MenuItem();
				ma2.Header = "Underline";
				ma2.Click += cntxMenuUnderline;
				contextmenu.Items.Add(ma2);

				var ma3 = new System.Windows.Controls.MenuItem();
				ma3.Header = "Strikeout";
				ma3.Click += cntxMenuStrike;
				contextmenu.Items.Add(ma3);*/

			}
			var m2 = new System.Windows.Controls.MenuItem();
			m2.Header = "Select Line";
			m2.Click += cntxMenuSelectLine;
			m2.Tag = info;
			contextmenu.Items.Add(m2); 
				
			var m3 = new System.Windows.Controls.MenuItem();
			m3.Header = "Select Block";
			m3.Click += cntxMenuSelectBlock;
			m3.Tag = info;
			contextmenu.Items.Add(m3);

			var m4 = new System.Windows.Controls.MenuItem();
			m4.Header = "Select Page";
			m4.Click += cntxMenuSelectPage;
			m4.Tag = info;
			contextmenu.Items.Add(m4);

			var m5 = new System.Windows.Controls.MenuItem();
			m5.Header = "Select All";
			m5.Click += cntxMenuSelectAll;
			contextmenu.Items.Add(m5);
		}

		private void cntxMenuCopy(object sender, RoutedEventArgs e)
		{
			/* Go through and get each line of text */
			String result = null;

			for (int kk = 0; kk < m_textSelect.Count; kk++)
			{
				var lines = m_lineptrs[m_textSelect[kk].pagenum];
				for (int jj = 0; jj < lines.Count; jj++)
				{
					var text = lines[jj].TextCharacters;
					for (int mm = 0; mm < text.Count; mm++)
					{
						result += text[mm].character;
					}
					result += "\r\n";
				}
			}
			System.Windows.Clipboard.SetText(result);
		}

		private void cntxMenuSelectLine(object sender, RoutedEventArgs e)
		{
			var mi = sender as System.Windows.Controls.MenuItem;
			ContextMenu_t info = (ContextMenu_t)mi.Tag;
			var page = m_docPages[info.page_num];

			InitTextSelection(page);

			page.SelX = 0;
			page.SelY = info.mouse_position.Y - 1;
			page.SelAnchorX = 0;
			page.SelAnchorY = info.mouse_position.Y - 1;
			page.SelColor = m_regionselect;

			/* Create new holder for lines highlighted */
			m_lineptrs[page.PageNum] = new LinesText();

			Point pos = new Point();
			pos.X = page.Width;
			pos.Y += info.mouse_position.Y + 1;

			UpdateSelection(pos, page);
			CheckIfSelected();
		}

		/* This one requires its own special handling TODO FIXME */
		private void cntxMenuSelectBlock(object sender, RoutedEventArgs e)
		{
			var mi = sender as System.Windows.Controls.MenuItem;
			ContextMenu_t info = (ContextMenu_t)mi.Tag;
			var page = m_docPages[info.page_num];
			bool found = false;
			int jj;

			InitTextSelection(page);

			/* Find the block that we are in */
			for (jj = 0; jj < page.TextBlocks.Count; jj++)
			{
				var intersect_blk = page.TextBlocks[jj].CheckIntersection(info.mouse_position.X, info.mouse_position.Y, 1, 1);
				if (intersect_blk != Intersection_t.NONE)
				{
					found = true;
					break;
				}
			}
			if (found)
			{
				page.SelX = page.TextBlocks[jj].X;
				page.SelY = page.TextBlocks[jj].Y;
				page.SelAnchorX = page.TextBlocks[jj].X;
				page.SelAnchorY = page.TextBlocks[jj].Y;
				page.SelColor = m_regionselect;

				/* Create new holder for lines highlighted */
				m_lineptrs[page.PageNum] = new LinesText();

				Point pos = new Point();
				pos.X = page.TextBlocks[jj].X + page.TextBlocks[jj].Width;
				pos.Y = page.TextBlocks[jj].Y + page.TextBlocks[jj].Height;

				UpdateSelection(pos, page);
				CheckIfSelected();
			}
			else
				m_textselected = false;
		}

		private void SelectFullPage(int page_num)
		{
			var page = m_docPages[page_num];

			InitTextSelection(page);

			page.SelX = 0;
			page.SelY = 0;
			page.SelAnchorX = 0;
			page.SelAnchorY = 0;
			page.SelColor = m_regionselect;

			/* Create new holder for lines highlighted */
			m_lineptrs[page.PageNum] = new LinesText();

			Point pos = new Point();
			pos.X = page.Width;
			pos.Y = page.Height;

			UpdateSelection(pos, page);
		}

		private void cntxMenuSelectPage(object sender, RoutedEventArgs e)
		{
			var mi = sender as System.Windows.Controls.MenuItem;
			ContextMenu_t info = (ContextMenu_t)mi.Tag;

			SelectFullPage(info.page_num);
			CheckIfSelected();
		}

		/* We need to await on the render range TODO FIXME */
		private void cntxMenuSelectAll(object sender, RoutedEventArgs e)
		{
			var mi = sender as System.Windows.Controls.MenuItem;
			if (m_textSelect != null)
				ClearSelections();
			else
				m_textSelect = new List<textSelectInfo_t>();

			/* Do first one and then the rest occur as new pages */
			/* Note that we have to render the pages TODO FIXME */
			SelectFullPage(0);
			for (int kk = 1; kk < m_num_pages; kk++)
			{
				if (!m_textset[kk])
					RenderRange(kk, false);
				var page = m_docPages[kk];
				Point pos = new Point();
				pos.X = page.Width;
				pos.Y = page.Height;
				UpdateSelection(pos, page);
			}
			CheckIfSelected();
		}

		private void cntxMenuDeselectAll(object sender, RoutedEventArgs e)
		{
			ClearSelections();
		}

		/* To add with annotation support */
		/*
		private void cntxMenuHighlight(object sender, RoutedEventArgs e)
		{
		
		}

		private void cntxMenuUnderline(object sender, RoutedEventArgs e)
		{

		}

		private void cntxMenuStrike(object sender, RoutedEventArgs e)
		{

		}
		*/
		#endregion TextSelection

		private void OnAboutClick(object sender, RoutedEventArgs e)
		{

		}

		private void OnHelpClick(object sender, RoutedEventArgs e)
		{

		}

		private void ExtractPages(object sender, RoutedEventArgs e)
		{

		}
	}
}