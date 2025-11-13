namespace EliteRentalsAPI.Helpers
{
    public static class EmailTemplateHelper
    {
        private const string BrandColor = "#2E86C1"; // Elite Rentals Blue
        private const string AccentColor = "#1ABC9C"; // Accent teal

        public static string WrapEmail(string title, string messageBody, string applicationLink = null)
        {
            string linkSection = string.IsNullOrEmpty(applicationLink)
                ? ""
                : $@"<p style='margin-top: 20px; text-align: center;'>
                        <a href='{applicationLink}' style='color: {AccentColor}; text-decoration: underline; font-weight: bold;'>
                            View Your Application Status
                        </a>
                    </p>";

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
                            margin: 30px auto;
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
                            padding: 15px;
                            font-size: 12px;
                            color: #666;
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
                            {linkSection}
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
