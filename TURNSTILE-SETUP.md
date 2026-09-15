# Cloudflare Turnstile Setup

1. In Cloudflare, open **Turnstile** and create a widget.
2. Add these hostnames to the widget:
   - `www.sdmtek.com`
   - `sdmtek.com`
   - `localhost`
3. Set the widget type to **Managed**.
4. Copy the widget's public site key into `frontend/public/turnstile-config.json`:

   ```json
   {
     "siteKey": "YOUR_TURNSTILE_SITE_KEY"
   }
   ```

5. In Railway, set the private secret as an environment variable named:

   ```text
   Turnstile__SecretKey
   ```

6. Deploy the frontend and backend together. The frontend public configuration is included in its build output.

The secret key must never be added to a committed configuration file or exposed to the browser.
