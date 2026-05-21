# APOD Wallpaper Website Deployment Guide

This guide describes how to publish the static APOD Wallpaper landing page to a VPS with Cloudflare DNS and nginx.

Recommended hostname:

```text
apod_wallpaper.p4kon.com
```

Alternative hostname:

```text
apod.p4kon.com
```

The current website source lives in:

```text
website/
```

## 1. What to send back before choosing ports

Run these commands on the VPS and send the output before we choose a custom internal port:

```bash
uname -a
cat /etc/os-release
```

```bash
sudo ss -tulpn
```

```bash
sudo systemctl status nginx --no-pager
nginx -v
sudo nginx -T 2>/tmp/nginx-dump.txt
sudo grep -R "server_name\\|listen" /etc/nginx/sites-enabled /etc/nginx/conf.d 2>/dev/null
```

```bash
sudo ufw status verbose || true
sudo iptables -S | head -80 || true
```

```bash
id apodsite || true
getent passwd apodsite || true
```

Notes:

- If nginx already owns ports `80` and `443`, that is good. We can add another `server_name` on the same public ports.
- A static website does not need an application port at all if nginx serves files directly.
- A custom internal port is only needed if we decide to run a separate service behind nginx.

## 2. Cloudflare DNS setup

Official Cloudflare DNS record flow: https://developers.cloudflare.com/dns/manage-dns-records/how-to/create-dns-records/

Steps:

1. Open Cloudflare dashboard.
2. Select `p4kon.com`.
3. Open `DNS` -> `Records`.
4. Click `Add record`.
5. Add one of these:

For an IPv4 VPS:

```text
Type: A
Name: apod_wallpaper
IPv4 address: YOUR_VPS_IPV4
Proxy status: Proxied
TTL: Auto
```

For a CNAME to an existing host:

```text
Type: CNAME
Name: apod_wallpaper
Target: existing-host.p4kon.com
Proxy status: Proxied
TTL: Auto
```

6. Save.
7. Wait for DNS propagation.

Check from your local machine:

```powershell
nslookup apod_wallpaper.p4kon.com
```

## 3. Create a dedicated Linux user

Run on VPS:

```bash
sudo adduser --system --group --home /var/www/apod-wallpaper apodsite
```

Create directories:

```bash
sudo mkdir -p /var/www/apod-wallpaper/site
sudo chown -R apodsite:apodsite /var/www/apod-wallpaper
sudo chmod -R 755 /var/www/apod-wallpaper
```

## 4. Upload the website

From your local machine, from repository root:

```powershell
scp -r .\website\* USER@YOUR_VPS_IP:/tmp/apod-wallpaper-site/
```

On VPS:

```bash
sudo rsync -av --delete /tmp/apod-wallpaper-site/ /var/www/apod-wallpaper/site/
sudo chown -R apodsite:apodsite /var/www/apod-wallpaper/site
sudo find /var/www/apod-wallpaper/site -type d -exec chmod 755 {} \;
sudo find /var/www/apod-wallpaper/site -type f -exec chmod 644 {} \;
```

## 5. Recommended nginx setup: serve static files directly

This is the simplest and best option.

Create config:

```bash
sudo nano /etc/nginx/sites-available/apod-wallpaper.conf
```

Paste:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name apod_wallpaper.p4kon.com;

    root /var/www/apod-wallpaper/site;
    index index.html;

    access_log /var/log/nginx/apod-wallpaper.access.log;
    error_log /var/log/nginx/apod-wallpaper.error.log;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location ~* \.(?:css|js|png|jpg|jpeg|webp|ico|svg)$ {
        expires 30d;
        add_header Cache-Control "public, max-age=2592000";
        try_files $uri =404;
    }
}
```

Enable:

```bash
sudo ln -s /etc/nginx/sites-available/apod-wallpaper.conf /etc/nginx/sites-enabled/apod-wallpaper.conf
sudo nginx -t
sudo systemctl reload nginx
```

Open:

```text
http://apod_wallpaper.p4kon.com
```

## 6. HTTPS

If Cloudflare is proxied, the fastest safe setup is:

1. Cloudflare SSL/TLS mode: `Full`.
2. Install an origin certificate or use Let's Encrypt on the VPS.

For Let's Encrypt with nginx:

```bash
sudo apt update
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d apod_wallpaper.p4kon.com
```

Certbot documentation: https://eff-certbot.readthedocs.io/en/stable/using.html

After certbot:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

## 7. Alternative setup: reverse proxy to an internal port

Use this only if you decide to run a local static server or app service.

Suggested candidate internal port:

```text
18081
```

But choose only after checking `sudo ss -tulpn`.

Example local static server service:

```bash
sudo nano /etc/systemd/system/apod-wallpaper-site.service
```

```ini
[Unit]
Description=APOD Wallpaper static website
After=network.target

[Service]
Type=simple
User=apodsite
Group=apodsite
WorkingDirectory=/var/www/apod-wallpaper/site
ExecStart=/usr/bin/python3 -m http.server 18081 --bind 127.0.0.1
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
```

Enable:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now apod-wallpaper-site
sudo systemctl status apod-wallpaper-site --no-pager
```

nginx reverse proxy config:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name apod_wallpaper.p4kon.com;

    access_log /var/log/nginx/apod-wallpaper.access.log;
    error_log /var/log/nginx/apod-wallpaper.error.log;

    location / {
        proxy_pass http://127.0.0.1:18081;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

nginx reverse proxy reference: https://docs.nginx.com/nginx/admin-guide/web-server/reverse-proxy/

## 8. Screenshot assets

Replace these files before publishing:

```text
website/assets/screenshot-main.png
website/assets/screenshot-settings.png
website/assets/screenshot-api-key.png
website/assets/screenshot-about.png
```

Current page expects those names.

## 9. Donation block

The website currently contains a conservative support section without active donation links.

Before adding crypto/card donation links:

- keep donations optional
- do not promise features, priority support, ad removal, or unlocks
- do not put direct donation links in the app before Store approval
- keep the app About page pointed to the website, support email, and policy pages

Microsoft Store policy should be reviewed again before adding a donation button inside the app.

## 10. Quick update flow after first deployment

Local machine:

```powershell
scp -r .\website\* USER@YOUR_VPS_IP:/tmp/apod-wallpaper-site/
```

VPS:

```bash
sudo rsync -av --delete /tmp/apod-wallpaper-site/ /var/www/apod-wallpaper/site/
sudo chown -R apodsite:apodsite /var/www/apod-wallpaper/site
sudo nginx -t
sudo systemctl reload nginx
```
