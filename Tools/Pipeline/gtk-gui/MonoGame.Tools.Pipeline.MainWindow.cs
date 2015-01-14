
// This file has been generated by the GUI designer. Do not modify.
namespace MonoGame.Tools.Pipeline
{
	internal partial class MainWindow
	{
		private global::Gtk.UIManager UIManager;
		
		private global::Gtk.Action FileAction;
		
		private global::Gtk.Action NewAction;
		
		private global::Gtk.Action OpenAction;
		
		private global::Gtk.Action OpenRecentAction;
		
		private global::Gtk.Action CloseAction;
		
		private global::Gtk.Action ImportAction;
		
		private global::Gtk.Action SaveAction;
		
		private global::Gtk.Action SaveAsAction;
		
		private global::Gtk.Action ExitAction;
		
		private global::Gtk.Action EditAction;
		
		private global::Gtk.Action UndoAction;
		
		private global::Gtk.Action RedoAction;
		
		private global::Gtk.Action NewItemAction;
		
		private global::Gtk.Action AddItemAction;
		
		private global::Gtk.Action DeleteAction;
		
		private global::Gtk.Action BuildAction;
		
		private global::Gtk.Action BuildAction1;
		
		private global::Gtk.Action RebuildAction;
		
		private global::Gtk.Action CleanAction;
		
		private global::Gtk.Action DebugModeAction;
		
		private global::Gtk.Action HelpAction;
		
		private global::Gtk.Action ViewHelpAction;
		
		private global::Gtk.Action AboutAction;
		
		private global::Gtk.Action CancelBuildAction;
		
		private global::Gtk.VBox vbox2;
		
		private global::Gtk.MenuBar menubar1;
		
		private global::Gtk.HPaned hpaned1;
		
		private global::Gtk.VPaned vpaned2;
		
		private global::MonoGame.Tools.Pipeline.ProjectView projectview1;
		
		private global::MonoGame.Tools.Pipeline.PropertiesView propertiesview1;
		
		private global::Gtk.ScrolledWindow GtkScrolledWindow;
		
		private global::Gtk.TextView textview2;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget MonoGame.Tools.Pipeline.MainWindow
			this.UIManager = new global::Gtk.UIManager ();
			global::Gtk.ActionGroup w1 = new global::Gtk.ActionGroup ("Default");
			this.FileAction = new global::Gtk.Action ("FileAction", global::Mono.Unix.Catalog.GetString ("File"), null, null);
			this.FileAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("File");
			w1.Add (this.FileAction, null);
			this.NewAction = new global::Gtk.Action ("NewAction", global::Mono.Unix.Catalog.GetString ("New..."), null, null);
			this.NewAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("New...");
			w1.Add (this.NewAction, "<Primary><Mod2>n");
			this.OpenAction = new global::Gtk.Action ("OpenAction", global::Mono.Unix.Catalog.GetString ("Open..."), null, null);
			this.OpenAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Open...");
			w1.Add (this.OpenAction, "<Primary><Mod2>o");
			this.OpenRecentAction = new global::Gtk.Action ("OpenRecentAction", global::Mono.Unix.Catalog.GetString ("Open Recent"), null, null);
			this.OpenRecentAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Open Recent");
			w1.Add (this.OpenRecentAction, null);
			this.CloseAction = new global::Gtk.Action ("CloseAction", global::Mono.Unix.Catalog.GetString ("Close"), null, null);
			this.CloseAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Close");
			w1.Add (this.CloseAction, null);
			this.ImportAction = new global::Gtk.Action ("ImportAction", global::Mono.Unix.Catalog.GetString ("Import..."), null, null);
			this.ImportAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Import...");
			w1.Add (this.ImportAction, null);
			this.SaveAction = new global::Gtk.Action ("SaveAction", global::Mono.Unix.Catalog.GetString ("Save"), null, null);
			this.SaveAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Save");
			w1.Add (this.SaveAction, "<Primary><Mod2>s");
			this.SaveAsAction = new global::Gtk.Action ("SaveAsAction", global::Mono.Unix.Catalog.GetString ("Save As..."), null, null);
			this.SaveAsAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Save As...");
			w1.Add (this.SaveAsAction, null);
			this.ExitAction = new global::Gtk.Action ("ExitAction", global::Mono.Unix.Catalog.GetString ("Exit"), null, null);
			this.ExitAction.HideIfEmpty = false;
			this.ExitAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Exit");
			w1.Add (this.ExitAction, null);
			this.EditAction = new global::Gtk.Action ("EditAction", global::Mono.Unix.Catalog.GetString ("Edit"), null, null);
			this.EditAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Edit");
			w1.Add (this.EditAction, null);
			this.UndoAction = new global::Gtk.Action ("UndoAction", global::Mono.Unix.Catalog.GetString ("Undo"), null, null);
			this.UndoAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Undo");
			w1.Add (this.UndoAction, "<Primary><Mod2>z");
			this.RedoAction = new global::Gtk.Action ("RedoAction", global::Mono.Unix.Catalog.GetString ("Redo"), null, null);
			this.RedoAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Redo");
			w1.Add (this.RedoAction, "<Primary><Mod2>y");
			this.NewItemAction = new global::Gtk.Action ("NewItemAction", global::Mono.Unix.Catalog.GetString ("New Item..."), null, null);
			this.NewItemAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("New Item...");
			w1.Add (this.NewItemAction, null);
			this.AddItemAction = new global::Gtk.Action ("AddItemAction", global::Mono.Unix.Catalog.GetString ("Add Item..."), null, null);
			this.AddItemAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Add Item...");
			w1.Add (this.AddItemAction, null);
			this.DeleteAction = new global::Gtk.Action ("DeleteAction", global::Mono.Unix.Catalog.GetString ("Delete"), null, null);
			this.DeleteAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Delete");
			w1.Add (this.DeleteAction, null);
			this.BuildAction = new global::Gtk.Action ("BuildAction", global::Mono.Unix.Catalog.GetString ("Build"), null, null);
			this.BuildAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Build");
			w1.Add (this.BuildAction, null);
			this.BuildAction1 = new global::Gtk.Action ("BuildAction1", global::Mono.Unix.Catalog.GetString ("Build"), null, null);
			this.BuildAction1.ShortLabel = global::Mono.Unix.Catalog.GetString ("Build");
			w1.Add (this.BuildAction1, "<Mod2>F6");
			this.RebuildAction = new global::Gtk.Action ("RebuildAction", global::Mono.Unix.Catalog.GetString ("Rebuild"), null, null);
			this.RebuildAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Rebuild");
			w1.Add (this.RebuildAction, null);
			this.CleanAction = new global::Gtk.Action ("CleanAction", global::Mono.Unix.Catalog.GetString ("Clean"), null, null);
			this.CleanAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Clean");
			w1.Add (this.CleanAction, null);
			this.DebugModeAction = new global::Gtk.Action ("DebugModeAction", global::Mono.Unix.Catalog.GetString ("Debug Mode"), null, null);
			this.DebugModeAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Debug Mode");
			w1.Add (this.DebugModeAction, null);
			this.HelpAction = new global::Gtk.Action ("HelpAction", global::Mono.Unix.Catalog.GetString ("Help"), null, null);
			this.HelpAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Help");
			w1.Add (this.HelpAction, null);
			this.ViewHelpAction = new global::Gtk.Action ("ViewHelpAction", global::Mono.Unix.Catalog.GetString ("View Help"), null, null);
			this.ViewHelpAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("View Help");
			w1.Add (this.ViewHelpAction, "<Mod2>F1");
			this.AboutAction = new global::Gtk.Action ("AboutAction", global::Mono.Unix.Catalog.GetString ("About"), null, null);
			this.AboutAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("About");
			w1.Add (this.AboutAction, null);
			this.CancelBuildAction = new global::Gtk.Action ("CancelBuildAction", global::Mono.Unix.Catalog.GetString ("Cancel Build"), null, null);
			this.CancelBuildAction.ShortLabel = global::Mono.Unix.Catalog.GetString ("Cancel Build");
			w1.Add (this.CancelBuildAction, null);
			this.UIManager.InsertActionGroup (w1, 0);
			this.AddAccelGroup (this.UIManager.AccelGroup);
			this.Name = "MonoGame.Tools.Pipeline.MainWindow";
			this.Title = global::Mono.Unix.Catalog.GetString ("Monogame Pipeline");
			this.Icon = global::Gdk.Pixbuf.LoadFromResource ("MonoGame.Tools.Pipeline.App.ico");
			this.WindowPosition = ((global::Gtk.WindowPosition)(4));
			// Container child MonoGame.Tools.Pipeline.MainWindow.Gtk.Container+ContainerChild
			this.vbox2 = new global::Gtk.VBox ();
			this.vbox2.Name = "vbox2";
			// Container child vbox2.Gtk.Box+BoxChild
			this.UIManager.AddUiFromString ("<ui><menubar name='menubar1'><menu name='FileAction' action='FileAction'><menuitem name='NewAction' action='NewAction'/><menuitem name='OpenAction' action='OpenAction'/><menuitem name='OpenRecentAction' action='OpenRecentAction'/><menuitem name='CloseAction' action='CloseAction'/><separator/><menuitem name='ImportAction' action='ImportAction'/><separator/><menuitem name='SaveAction' action='SaveAction'/><menuitem name='SaveAsAction' action='SaveAsAction'/><separator/><menuitem name='ExitAction' action='ExitAction'/></menu><menu name='EditAction' action='EditAction'><menuitem name='UndoAction' action='UndoAction'/><menuitem name='RedoAction' action='RedoAction'/><separator/><menuitem name='NewItemAction' action='NewItemAction'/><menuitem name='AddItemAction' action='AddItemAction'/><separator/><menuitem name='DeleteAction' action='DeleteAction'/></menu><menu name='BuildAction' action='BuildAction'><menuitem name='BuildAction1' action='BuildAction1'/><menuitem name='RebuildAction' action='RebuildAction'/><menuitem name='CleanAction' action='CleanAction'/><menuitem name='CancelBuildAction' action='CancelBuildAction'/></menu><menu name='HelpAction' action='HelpAction'><menuitem name='ViewHelpAction' action='ViewHelpAction'/><separator/><menuitem name='AboutAction' action='AboutAction'/></menu></menubar></ui>");
			this.menubar1 = ((global::Gtk.MenuBar)(this.UIManager.GetWidget ("/menubar1")));
			this.menubar1.Name = "menubar1";
			this.vbox2.Add (this.menubar1);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.menubar1]));
			w2.Position = 0;
			w2.Expand = false;
			w2.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.hpaned1 = new global::Gtk.HPaned ();
			this.hpaned1.CanFocus = true;
			this.hpaned1.Name = "hpaned1";
			this.hpaned1.Position = 179;
			// Container child hpaned1.Gtk.Paned+PanedChild
			this.vpaned2 = new global::Gtk.VPaned ();
			this.vpaned2.CanFocus = true;
			this.vpaned2.Name = "vpaned2";
			this.vpaned2.Position = 247;
			// Container child vpaned2.Gtk.Paned+PanedChild
			this.projectview1 = new global::MonoGame.Tools.Pipeline.ProjectView ();
			this.projectview1.Events = ((global::Gdk.EventMask)(256));
			this.projectview1.Name = "projectview1";
			this.vpaned2.Add (this.projectview1);
			global::Gtk.Paned.PanedChild w3 = ((global::Gtk.Paned.PanedChild)(this.vpaned2 [this.projectview1]));
			w3.Resize = false;
			// Container child vpaned2.Gtk.Paned+PanedChild
			this.propertiesview1 = new global::MonoGame.Tools.Pipeline.PropertiesView ();
			this.propertiesview1.Events = ((global::Gdk.EventMask)(256));
			this.propertiesview1.Name = "propertiesview1";
			this.vpaned2.Add (this.propertiesview1);
			global::Gtk.Paned.PanedChild w4 = ((global::Gtk.Paned.PanedChild)(this.vpaned2 [this.propertiesview1]));
			w4.Resize = false;
			this.hpaned1.Add (this.vpaned2);
			global::Gtk.Paned.PanedChild w5 = ((global::Gtk.Paned.PanedChild)(this.hpaned1 [this.vpaned2]));
			w5.Resize = false;
			// Container child hpaned1.Gtk.Paned+PanedChild
			this.GtkScrolledWindow = new global::Gtk.ScrolledWindow ();
			this.GtkScrolledWindow.Name = "GtkScrolledWindow";
			this.GtkScrolledWindow.ShadowType = ((global::Gtk.ShadowType)(1));
			// Container child GtkScrolledWindow.Gtk.Container+ContainerChild
			this.textview2 = new global::Gtk.TextView ();
			this.textview2.CanFocus = true;
			this.textview2.Name = "textview2";
			this.textview2.Editable = false;
			this.GtkScrolledWindow.Add (this.textview2);
			this.hpaned1.Add (this.GtkScrolledWindow);
			this.vbox2.Add (this.hpaned1);
			global::Gtk.Box.BoxChild w8 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.hpaned1]));
			w8.Position = 1;
			this.Add (this.vbox2);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.DefaultWidth = 751;
			this.DefaultHeight = 557;
			this.Show ();
			this.DeleteEvent += new global::Gtk.DeleteEventHandler (this.OnDeleteEvent);
			this.FileAction.Activated += new global::System.EventHandler (this.OnFileActionActivated);
			this.NewAction.Activated += new global::System.EventHandler (this.OnNewActionActivated);
			this.OpenAction.Activated += new global::System.EventHandler (this.OnOpenActionActivated);
			this.CloseAction.Activated += new global::System.EventHandler (this.OnCloseActionActivated);
			this.ImportAction.Activated += new global::System.EventHandler (this.OnImportActionActivated);
			this.SaveAction.Activated += new global::System.EventHandler (this.OnSaveActionActivated);
			this.SaveAsAction.Activated += new global::System.EventHandler (this.OnSaveAsActionActivated);
			this.ExitAction.Activated += new global::System.EventHandler (this.OnExitActionActivated);
			this.UndoAction.Activated += new global::System.EventHandler (this.OnUndoActionActivated);
			this.RedoAction.Activated += new global::System.EventHandler (this.OnRedoActionActivated);
			this.NewItemAction.Activated += new global::System.EventHandler (this.OnNewItemActionActivated);
			this.AddItemAction.Activated += new global::System.EventHandler (this.OnAddItemActionActivated);
			this.DeleteAction.Activated += new global::System.EventHandler (this.OnDeleteActionActivated);
			this.BuildAction.Activated += new global::System.EventHandler (this.OnBuildActionActivated);
			this.BuildAction1.Activated += new global::System.EventHandler (this.OnBuildAction1Activated);
			this.RebuildAction.Activated += new global::System.EventHandler (this.OnRebuildActionActivated);
			this.CleanAction.Activated += new global::System.EventHandler (this.OnCleanActionActivated);
			this.ViewHelpAction.Activated += new global::System.EventHandler (this.OnViewHelpActionActivated);
			this.AboutAction.Activated += new global::System.EventHandler (this.OnAboutActionActivated);
		}
	}
}
