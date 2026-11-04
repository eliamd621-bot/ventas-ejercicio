using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

class User { public string Name, Hash, Role; }
class Product { public int Id; public string Code, Name; public decimal Price; public int Stock; }
class Line { public int Id; public string Name; public int Qty; public decimal Price; public decimal Total { get { return Qty * Price; } } }
class Invoice { public int No; public DateTime Date; public string Customer, User; public List<Line> Lines = new List<Line>();
  public decimal Sub { get { return Lines.Sum(l => l.Total); } } public decimal Tax { get { return Math.Round(Sub * 0.18m, 2); } } public decimal Total { get { return Sub + Tax; } } }

static class Db {
  public static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PuntoDeVentaCS");
  public static List<User> Users = new List<User>(); public static List<Product> Products = new List<Product>(); public static List<Invoice> Invoices = new List<Invoice>();
  static CultureInfo C = CultureInfo.InvariantCulture;
  public static string Hash(string p) { using (var s = SHA256.Create()) return BitConverter.ToString(s.ComputeHash(Encoding.UTF8.GetBytes("pos:" + p))).Replace("-", ""); }
  public static string Clean(string s) { return (s ?? "").Replace('\t', ' ').Replace('|', ' ').Replace(';', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim(); }
  static string F(string n) { return Path.Combine(Dir, n); }
  public static void Load() {
    Directory.CreateDirectory(Dir);
    if (File.Exists(F("users.txt"))) foreach (var l in File.ReadAllLines(F("users.txt"))) { var p = l.Split('\t'); if (p.Length == 3) Users.Add(new User { Name = p[0], Hash = p[1], Role = p[2] }); }
    if (Users.Count == 0) { Users.Add(new User { Name = "admin", Hash = Hash("admin123"), Role = "admin" }); SaveUsers(); }
    if (File.Exists(F("products.txt"))) foreach (var l in File.ReadAllLines(F("products.txt"))) { var p = l.Split('\t'); if (p.Length == 5) Products.Add(new Product { Id = int.Parse(p[0]), Code = p[1], Name = p[2], Price = decimal.Parse(p[3], C), Stock = int.Parse(p[4]) }); }
    else { Products.Add(new Product { Id = 1, Code = "001", Name = "Agua 500ml", Price = 30, Stock = 100 }); Products.Add(new Product { Id = 2, Code = "002", Name = "Refresco 1L", Price = 75, Stock = 60 }); SaveProducts(); }
    if (File.Exists(F("invoices.txt"))) foreach (var l in File.ReadAllLines(F("invoices.txt"))) {
      var p = l.Split('\t'); if (p.Length != 5) continue;
      var v = new Invoice { No = int.Parse(p[0]), Date = DateTime.Parse(p[1], C), Customer = p[2], User = p[3] };
      foreach (var x in p[4].Split(';')) { var q = x.Split('|'); if (q.Length == 4) v.Lines.Add(new Line { Id = int.Parse(q[0]), Name = q[1], Qty = int.Parse(q[2]), Price = decimal.Parse(q[3], C) }); }
      Invoices.Add(v);
    }
  }
  public static void SaveUsers() { File.WriteAllLines(F("users.txt"), Users.Select(u => u.Name + "\t" + u.Hash + "\t" + u.Role).ToArray()); }
  public static void SaveProducts() { File.WriteAllLines(F("products.txt"), Products.Select(p => p.Id + "\t" + p.Code + "\t" + p.Name + "\t" + p.Price.ToString(C) + "\t" + p.Stock).ToArray()); }
  public static void SaveInvoices() { File.WriteAllLines(F("invoices.txt"), Invoices.Select(v => v.No + "\t" + v.Date.ToString("s", C) + "\t" + v.Customer + "\t" + v.User + "\t" + string.Join(";", v.Lines.Select(l => l.Id + "|" + l.Name + "|" + l.Qty + "|" + l.Price.ToString(C)).ToArray())).ToArray()); }
  public static int NextInvoice() { return Invoices.Count == 0 ? 1 : Invoices.Max(i => i.No) + 1; }
}

static class Printer {
  public static string Money(decimal d) { return d.ToString("N2", new CultureInfo("es-DO")); }
  public static string ToText(Invoice v) {
    int W = 42; Func<string, string, string> R = (a, b) => a + new string(' ', Math.Max(1, W - a.Length - b.Length)) + b;
    Func<string, string> Cn = s => new string(' ', Math.Max(0, (W - s.Length) / 2)) + s; var sb = new List<string>();
    sb.Add(Cn("PUNTO DE VENTA")); sb.Add(Cn("FACTURA No. " + v.No.ToString("D6"))); sb.Add(new string('-', W));
    sb.Add("Fecha: " + v.Date.ToString("dd/MM/yyyy HH:mm")); sb.Add("Cliente: " + v.Customer); sb.Add("Atendido por: " + v.User); sb.Add(new string('-', W));
    foreach (var l in v.Lines) { sb.Add(l.Name); sb.Add(R("  " + l.Qty + " x " + Money(l.Price), Money(l.Total))); }
    sb.Add(new string('-', W)); sb.Add(R("Subtotal", Money(v.Sub))); sb.Add(R("ITBIS 18%", Money(v.Tax))); sb.Add(R("TOTAL RD$", Money(v.Total))); sb.Add(new string('-', W)); sb.Add(Cn("Gracias por su compra"));
    return string.Join("\r\n", sb.ToArray());
  }
  // PDF minimo (texto Courier) sin librerias externas
  public static void ToPdf(string path, string text) {
    var lines = text.Replace("\r", "").Split('\n'); var content = new StringBuilder("BT /F1 11 Tf 14 TL 50 790 Td\n");
    foreach (var l in lines) content.Append("(" + l.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + ") Tj T*\n");
    content.Append("ET"); var enc = Encoding.GetEncoding("iso-8859-1"); var body = enc.GetBytes(content.ToString());
    var objs = new List<byte[]>();
    objs.Add(enc.GetBytes("<< /Type /Catalog /Pages 2 0 R >>")); objs.Add(enc.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));
    objs.Add(enc.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>"));
    var o4 = new List<byte>(); o4.AddRange(enc.GetBytes("<< /Length " + body.Length + " >>\nstream\n")); o4.AddRange(body); o4.AddRange(enc.GetBytes("\nendstream")); objs.Add(o4.ToArray());
    objs.Add(enc.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>"));
    using (var fs = new MemoryStream()) {
      Action<string> W = s => { var b = enc.GetBytes(s); fs.Write(b, 0, b.Length); }; var offs = new List<long>(); W("%PDF-1.4\n");
      for (int i = 0; i < objs.Count; i++) { offs.Add(fs.Position); W((i + 1) + " 0 obj\n"); fs.Write(objs[i], 0, objs[i].Length); W("\nendobj\n"); }
      long xr = fs.Position; W("xref\n0 " + (objs.Count + 1) + "\n0000000000 65535 f \n"); foreach (var o in offs) W(o.ToString("D10") + " 00000 n \n");
      W("trailer\n<< /Size " + (objs.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + xr + "\n%%EOF"); File.WriteAllBytes(path, fs.ToArray());
    }
  }
  public static void Export(Invoice v) {
    if (v == null) { MessageBox.Show("Primero guarda una factura o selecciona una en la pestaña Facturas.", "Imprimir"); return; }
    using (var d = new SaveFileDialog { Filter = "Documento PDF (*.pdf)|*.pdf|Archivo de texto (*.txt)|*.txt", FileName = "Factura-" + v.No.ToString("D6") }) {
      if (d.ShowDialog() != DialogResult.OK) return;
      var t = ToText(v); if (d.FileName.ToLower().EndsWith(".txt")) File.WriteAllText(d.FileName, t, Encoding.UTF8); else ToPdf(d.FileName, t);
      MessageBox.Show("Factura guardada en:\n" + d.FileName, "Imprimir");
    }
  }
}

class LoginForm : Form {
  public User Result; TextBox u, p;
  public LoginForm() {
    Text = "Punto de Venta - Iniciar sesión"; ClientSize = new Size(320, 210); StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; Font = new Font("Segoe UI", 10);
    Controls.Add(new Label { Text = "Usuario", Left = 20, Top = 20, AutoSize = true }); u = new TextBox { Left = 20, Top = 42, Width = 280 }; Controls.Add(u);
    Controls.Add(new Label { Text = "Contraseña", Left = 20, Top = 78, AutoSize = true }); p = new TextBox { Left = 20, Top = 100, Width = 280, UseSystemPasswordChar = true }; Controls.Add(p);
    var b = new Button { Text = "Entrar", Left = 20, Top = 145, Width = 280, Height = 34 }; b.Click += (s, e) => Go(); Controls.Add(b); AcceptButton = b;
    Controls.Add(new Label { Text = "Primer acceso: admin / admin123", Left = 20, Top = 184, AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8) });
  }
  void Go() {
    var h = Db.Hash(p.Text); var f = Db.Users.FirstOrDefault(x => x.Name == u.Text.Trim() && x.Hash == h);
    if (f == null) { MessageBox.Show("Usuario o contraseña incorrectos.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning); p.Clear(); p.Focus(); return; }
    Result = f; DialogResult = DialogResult.OK;
  }
}

class MainForm : Form {
  User me; Invoice last; List<Line> cart = new List<Line>();
  ListView lvProd, lvCart, lvMan, lvInv, lvUsr; TextBox q, cust, mc, mn, mp, ms, nu, np; Label total; ComboBox nr;
  static ListView LV(int x, int y, int w, int h, params string[] cols) { var l = new ListView { Left = x, Top = y, Width = w, Height = h, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false, MultiSelect = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom }; foreach (var c in cols) l.Columns.Add(c, -2); return l; }
  static Button Bt(string t, int x, int y, int w, EventHandler h) { var b = new Button { Text = t, Left = x, Top = y, Width = w, Height = 30 }; b.Click += h; return b; }
  static Label Lb(string t, int x, int y) { return new Label { Text = t, Left = x, Top = y, AutoSize = true }; }
  static TextBox Tx(int x, int y, int w) { return new TextBox { Left = x, Top = y, Width = w }; }
  static void Fit(ListView l, int[] w) { for (int i = 0; i < w.Length; i++) l.Columns[i].Width = w[i]; }

  public MainForm(User u) {
    me = u; Text = "Punto de Venta - " + u.Name + (u.Role == "admin" ? " (administrador)" : ""); Size = new Size(1100, 640); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10); KeyPreview = true;
    var tabs = new TabControl { Dock = DockStyle.Fill }; Controls.Add(tabs);
    var tv = new TabPage("Nueva factura"); var tp = new TabPage("Artículos"); var th = new TabPage("Facturas"); tabs.TabPages.Add(tv); tabs.TabPages.Add(tp); tabs.TabPages.Add(th);
    // ---- Venta
    tv.Controls.Add(Lb("Buscar artículo:", 10, 12)); q = Tx(130, 8, 250); q.TextChanged += (s, e) => FillProd(); tv.Controls.Add(q);
    lvProd = LV(10, 40, 480, 480, "Código", "Artículo", "Precio", "Stock"); Fit(lvProd, new[] { 70, 230, 100, 60 }); lvProd.DoubleClick += (s, e) => AddSel(); tv.Controls.Add(lvProd);
    tv.Controls.Add(Bt("Agregar a la factura ►", 10, 525, 200, (s, e) => AddSel()));
    tv.Controls.Add(Lb("Cliente:", 510, 12)); cust = Tx(580, 8, 300); tv.Controls.Add(cust);
    lvCart = LV(510, 40, 540, 360, "Artículo", "Cant.", "Precio", "Total"); Fit(lvCart, new[] { 230, 60, 120, 120 }); tv.Controls.Add(lvCart);
    tv.Controls.Add(Bt("+1", 510, 405, 50, (s, e) => Chg(1))); tv.Controls.Add(Bt("-1", 565, 405, 50, (s, e) => Chg(-1))); tv.Controls.Add(Bt("Quitar", 620, 405, 80, (s, e) => Chg(-9999)));
    total = new Label { Left = 510, Top = 450, Width = 540, Height = 30, Font = new Font("Segoe UI", 15, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; tv.Controls.Add(total);
    tv.Controls.Add(Bt("Guardar factura", 510, 495, 160, (s, e) => Save())); tv.Controls.Add(Bt("Limpiar", 675, 495, 90, (s, e) => { cart.Clear(); cust.Clear(); FillCart(); }));
    tv.Controls.Add(Bt("Imprimir (F5)", 880, 495, 170, (s, e) => Printer.Export(last)));
    // ---- Articulos
    tp.Controls.Add(Lb("Código", 10, 12)); mc = Tx(10, 34, 90); tp.Controls.Add(mc); tp.Controls.Add(Lb("Nombre", 110, 12)); mn = Tx(110, 34, 250); tp.Controls.Add(mn);
    tp.Controls.Add(Lb("Precio", 370, 12)); mp = Tx(370, 34, 90); tp.Controls.Add(mp); tp.Controls.Add(Lb("Stock", 470, 12)); ms = Tx(470, 34, 80); tp.Controls.Add(ms);
    tp.Controls.Add(Bt("Agregar", 570, 31, 90, (s, e) => ProdSave(false))); tp.Controls.Add(Bt("Actualizar", 665, 31, 100, (s, e) => ProdSave(true)));
    if (me.Role == "admin") tp.Controls.Add(Bt("Eliminar", 770, 31, 90, (s, e) => ProdDel()));
    lvMan = LV(10, 75, 1040, 440, "Código", "Artículo", "Precio", "Stock"); Fit(lvMan, new[] { 100, 450, 150, 100 });
    lvMan.SelectedIndexChanged += (s, e) => { if (lvMan.SelectedItems.Count == 0) return; var p = (Product)lvMan.SelectedItems[0].Tag; mc.Text = p.Code; mn.Text = p.Name; mp.Text = p.Price.ToString(CultureInfo.CurrentCulture); ms.Text = p.Stock.ToString(); }; tp.Controls.Add(lvMan);
    // ---- Facturas
    lvInv = LV(10, 10, 1040, 470, "No.", "Fecha", "Cliente", "Usuario", "Total"); Fit(lvInv, new[] { 90, 160, 300, 150, 150 }); th.Controls.Add(lvInv);
    th.Controls.Add(Bt("Imprimir seleccionada (F5)", 10, 490, 230, (s, e) => Printer.Export(Sel()))); th.Controls.Add(Lb("Selecciona una factura y pulsa F5 para exportarla a .pdf o .txt.", 260, 497));
    // ---- Usuarios (solo admin)
    if (me.Role == "admin") {
      var tu = new TabPage("Usuarios"); tabs.TabPages.Add(tu);
      tu.Controls.Add(Lb("Usuario", 10, 12)); nu = Tx(10, 34, 160); tu.Controls.Add(nu); tu.Controls.Add(Lb("Contraseña", 180, 12)); np = Tx(180, 34, 160); np.UseSystemPasswordChar = true; tu.Controls.Add(np);
      tu.Controls.Add(Lb("Rol", 350, 12)); nr = new ComboBox { Left = 350, Top = 34, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList }; nr.Items.AddRange(new object[] { "usuario", "admin" }); nr.SelectedIndex = 0; tu.Controls.Add(nr);
      tu.Controls.Add(Bt("Crear", 500, 31, 80, (s, e) => UserAdd())); tu.Controls.Add(Bt("Eliminar seleccionado", 585, 31, 170, (s, e) => UserDel()));
      lvUsr = LV(10, 75, 600, 400, "Usuario", "Rol"); Fit(lvUsr, new[] { 300, 200 }); tu.Controls.Add(lvUsr); FillUsers();
    }
    KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { e.Handled = true; e.SuppressKeyPress = true; Printer.Export(tabs.SelectedTab == th ? Sel() : last); } };
    tabs.SelectedIndexChanged += (s, e) => { FillInv(); };
    FillProd(); FillMan(); FillCart(); FillInv();
  }
  Invoice Sel() { return lvInv.SelectedItems.Count > 0 ? (Invoice)lvInv.SelectedItems[0].Tag : last; }
  static ListViewItem It(object tag, params string[] t) { return new ListViewItem(t) { Tag = tag }; }
  void FillProd() { lvProd.Items.Clear(); foreach (var p in Db.Products.Where(x => (x.Code + " " + x.Name).ToLower().Contains(q.Text.ToLower()))) lvProd.Items.Add(It(p, p.Code, p.Name, Printer.Money(p.Price), p.Stock.ToString())); }
  void FillMan() { lvMan.Items.Clear(); foreach (var p in Db.Products) lvMan.Items.Add(It(p, p.Code, p.Name, Printer.Money(p.Price), p.Stock.ToString())); FillProd(); }
  void FillInv() { lvInv.Items.Clear(); foreach (var v in Db.Invoices.OrderByDescending(i => i.No)) lvInv.Items.Add(It(v, v.No.ToString("D6"), v.Date.ToString("dd/MM/yyyy HH:mm"), v.Customer, v.User, Printer.Money(v.Total))); }
  void FillUsers() { lvUsr.Items.Clear(); foreach (var u in Db.Users) lvUsr.Items.Add(It(u, u.Name, u.Role)); }
  void FillCart() {
    lvCart.Items.Clear(); foreach (var l in cart) lvCart.Items.Add(It(l, l.Name, l.Qty.ToString(), Printer.Money(l.Price), Printer.Money(l.Total)));
    var s = cart.Sum(l => l.Total); total.Text = "Total (ITBIS incl.): RD$ " + Printer.Money(s + Math.Round(s * 0.18m, 2));
  }
  void AddSel() {
    if (lvProd.SelectedItems.Count == 0) return; var p = (Product)lvProd.SelectedItems[0].Tag; var l = cart.FirstOrDefault(x => x.Id == p.Id);
    if (l == null) cart.Add(new Line { Id = p.Id, Name = p.Name, Price = p.Price, Qty = 1 }); else l.Qty++; FillCart();
  }
  void Chg(int d) {
    if (lvCart.SelectedItems.Count == 0) return; var l = (Line)lvCart.SelectedItems[0].Tag; l.Qty += d; if (l.Qty < 1) cart.Remove(l); FillCart();
  }
  void Save() {
    if (cart.Count == 0) return;
    foreach (var l in cart) { var p = Db.Products.First(x => x.Id == l.Id); if (p.Stock < l.Qty) { MessageBox.Show("Stock insuficiente de " + p.Name + " (disponible: " + p.Stock + ").", "Factura"); return; } }
    foreach (var l in cart) Db.Products.First(x => x.Id == l.Id).Stock -= l.Qty;
    var v = new Invoice { No = Db.NextInvoice(), Date = DateTime.Now, Customer = cust.Text.Trim() == "" ? "Consumidor final" : Db.Clean(cust.Text), User = me.Name, Lines = cart.Select(l => new Line { Id = l.Id, Name = Db.Clean(l.Name), Qty = l.Qty, Price = l.Price }).ToList() };
    Db.Invoices.Add(v); Db.SaveInvoices(); Db.SaveProducts(); last = v; cart.Clear(); cust.Clear(); FillCart(); FillMan(); FillInv();
    MessageBox.Show("Factura " + v.No.ToString("D6") + " guardada. Pulsa F5 para imprimirla (.pdf o .txt).", "Factura");
  }
  void ProdSave(bool update) {
    decimal pr; int st; if (mn.Text.Trim() == "" || !decimal.TryParse(mp.Text, out pr) || pr < 0) { MessageBox.Show("Escribe nombre y un precio válido."); return; }
    int.TryParse(ms.Text, out st);
    if (update) { if (lvMan.SelectedItems.Count == 0) { MessageBox.Show("Selecciona un artículo de la lista."); return; } var p = (Product)lvMan.SelectedItems[0].Tag; p.Code = Db.Clean(mc.Text); p.Name = Db.Clean(mn.Text); p.Price = pr; p.Stock = st; }
    else Db.Products.Add(new Product { Id = Db.Products.Count == 0 ? 1 : Db.Products.Max(x => x.Id) + 1, Code = Db.Clean(mc.Text), Name = Db.Clean(mn.Text), Price = pr, Stock = st });
    Db.SaveProducts(); FillMan(); mc.Clear(); mn.Clear(); mp.Clear(); ms.Clear();
  }
  void ProdDel() { if (lvMan.SelectedItems.Count == 0) return; if (MessageBox.Show("¿Eliminar el artículo?", "Confirmar", MessageBoxButtons.YesNo) != DialogResult.Yes) return; Db.Products.Remove((Product)lvMan.SelectedItems[0].Tag); Db.SaveProducts(); FillMan(); }
  void UserAdd() {
    var n = Db.Clean(nu.Text); if (n == "" || np.Text.Length < 4) { MessageBox.Show("Escribe usuario y contraseña (mínimo 4 caracteres)."); return; }
    if (Db.Users.Any(x => x.Name == n)) { MessageBox.Show("Ese usuario ya existe."); return; }
    Db.Users.Add(new User { Name = n, Hash = Db.Hash(np.Text), Role = nr.SelectedIndex == 1 ? "admin" : "usuario" }); Db.SaveUsers(); nu.Clear(); np.Clear(); FillUsers();
  }
  void UserDel() {
    if (lvUsr.SelectedItems.Count == 0) return; var u = (User)lvUsr.SelectedItems[0].Tag; if (u.Name == me.Name) { MessageBox.Show("No puedes eliminar tu propio usuario."); return; }
    if (MessageBox.Show("¿Eliminar al usuario " + u.Name + "?", "Confirmar", MessageBoxButtons.YesNo) != DialogResult.Yes) return; Db.Users.Remove(u); Db.SaveUsers(); FillUsers();
  }
}

static class Program {
  [STAThread] static void Main() {
    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Db.Load();
    using (var l = new LoginForm()) { if (l.ShowDialog() != DialogResult.OK) return; Application.Run(new MainForm(l.Result)); }
  }
}
