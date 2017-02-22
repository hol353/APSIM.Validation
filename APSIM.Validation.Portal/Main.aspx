<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Main.aspx.cs" Inherits="APSIM.Validation.Portal.Main" %>

<%@ Register assembly="System.Web.DataVisualization, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" namespace="System.Web.UI.DataVisualization.Charting" tagprefix="asp" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
        <p>
            <asp:Label ID="Label1" runat="server" Text="This web page allows you to view a models validation and compare it with the previous years validation."></asp:Label>
        </p>
        <p>
            <asp:Label ID="Label2" runat="server" Text="Model:  "></asp:Label>

            <asp:DropDownList ID="ModelList" runat="server" Height="39px" Width="272px" AutoPostBack="True">
            </asp:DropDownList>
            <asp:Label ID="Label3" runat="server" Text="Variable:  "></asp:Label>

            <asp:DropDownList ID="VariableList" runat="server" Height="39px" Width="272px" AutoPostBack="True">
            </asp:DropDownList>
        </p>
        <asp:Chart ID="Chart1" runat="server" Height="404px" Width="437px">
            <chartareas>
                <asp:ChartArea Name="ChartArea1">
                </asp:ChartArea>
            </chartareas>
        </asp:Chart>
        <asp:Chart ID="Chart2" runat="server" Height="404px" Width="437px">
            <chartareas>
                <asp:ChartArea Name="ChartArea1">
                </asp:ChartArea>
            </chartareas>
        </asp:Chart>
        <asp:gridview ID="GridView" runat="server" OnRowCommand="OnRowCommand" OnRowDataBound="GridView_RowDataBound">
            <RowStyle HorizontalAlign="Right" />
        </asp:gridview>
    </form>
</body>
</html>

