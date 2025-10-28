namespace EliteRentalsAPI.Helpers
{
    public static class EmailTemplateHelper
    {
        private const string BrandColor = "#2E86C1"; // Elite Rentals Blue
        private const string AccentColor = "#1ABC9C"; // Accent teal

        public static string WrapEmail(string title, string messageBody)
        {
            return $@"
            <html>
                <head>
                    <style>
                        body {{
                            font-family: 'Segoe UI', Arial, sans-serif;
                            background-color: #f8f9fa;
                            color: #333;
                            margin: 0;
                            padding: 0;
                        }}
                        .container {{
                            max-width: 600px;
                            margin: 20px auto;
                            background-color: #ffffff;
                            border-radius: 10px;
                            overflow: hidden;
                            box-shadow: 0 3px 10px rgba(0,0,0,0.1);
                        }}
                        .header {{
                            background-color: {BrandColor};
                            color: #ffffff;
                            padding: 20px;
                            text-align: center;
                        }}
                        .content {{
                            padding: 25px;
                            line-height: 1.6;
                        }}
                        .footer {{
                            background-color: #f1f1f1;
                            text-align: center;
                            padding: 10px;
                            font-size: 12px;
                            color: #666;
                        }}
                        .button {{
                            display: inline-block;
                            background-color: {AccentColor};
                            color: white;
                            padding: 10px 20px;
                            border-radius: 5px;
                            text-decoration: none;
                            margin-top: 10px;
                        }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>{title}</h2>
                        </div>
                        <div class='content'>
                            {messageBody}
                        </div>
                        <div class='footer'>
                            <p>© {DateTime.UtcNow.Year} Elite Rentals. All rights reserved.</p>
                            <p>This is an automated message, please do not reply directly.</p>
                        </div>
                    </div>
                </body>
            </html>";
        }
    }
}
