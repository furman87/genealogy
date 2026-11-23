# Genealogy Club Website

A website for Colonial Heritage neighbors in Williamsburg, Virginia. The home page introduces the club and shows upcoming meetings. The resource library holds meeting slides and videos. The administrator manages the site and creates member accounts as needed.

## Technology

- ASP.NET Core Razor Pages on .NET 10
- SQLite with Dapper for all database access
- Docker Compose and an Nginx reverse proxy on Ubuntu 24.04

SQLite keeps the site to one service. The app creates and upgrades its own schema at startup. The SQLite database, uploads, and sign-in keys live together in a persistent directory.

## Run locally

From PowerShell with the .NET 10 SDK installed:

```powershell
cd D:\Dev\Genealogy
dotnet run
```

Open [http://127.0.0.1:5186](http://127.0.0.1:5186). To create the first admin on a local database, stop the app, then enter the email and a password of at least 12 characters. These settings apply to the current PowerShell session.

```powershell
$env:BootstrapAdmin__Email = Read-Host 'Administrator email'
$env:BootstrapAdmin__Password = Read-Host -MaskInput 'Administrator password'
dotnet run
```

Sign in from the header to open **Club administration**. After the first admin is created, stop the app and remove the bootstrap settings from your PowerShell session with `Remove-Item Env:BootstrapAdmin__Email, Env:BootstrapAdmin__Password`. The account persists in `App_Data/club.db`.

## Manage the site

- **Homepage** edits the headline, opening paragraph, and footer email address. Select words and choose **Toggle highlight** to give them the gold italic style, or put asterisks around them like `*story.*`. Use a new line to break the headline. Leave the club email blank to hide it from the public footer.
- **Mission** updates the draft statement on the homepage.
- **Meetings** posts or edits meeting dates, times, and locations. The public schedule shows upcoming meetings in Eastern time.
- **Resources** accepts PDF or PPTX slides and MP4 or WebM videos up to 100 MB each. For public resources, you can also post an HTTPS link, such as a link to a hosted video. To protect material with a club login, upload the file and leave **Require a club sign-in** checked.
- **Members** creates accounts when the club is ready. Share each temporary password privately. Members can change it from **Account**. Resetting a password or revoking access ends the member’s existing sessions.

There are no sample meetings, posts, or member accounts. The draft mission can be replaced as soon as the club supplies its final wording.

## Deploy on Ubuntu 24.04

With Docker, the Compose plugin, and Nginx installed, clone your GitHub repository to `/opt/genealogy`. Replace the example GitHub owner and repository with your own.

```bash
sudo mkdir -p /opt/genealogy
sudo chown "$USER":"$USER" /opt/genealogy
git clone https://github.com/YOUR-OWNER/YOUR-REPOSITORY.git /opt/genealogy
cd /opt/genealogy
```

1. Check that port 5186 is free with `sudo ss -ltn 'sport = :5186'`. If it is in use, set a free port in `.env` and use the same value in the Nginx file.
2. Copy `.env.example` to `.env` and restrict access to it with `chmod 600 .env`. Add your public hostname to `CLUB_HOSTS`, separated from the loopback names by semicolons.

   ```dotenv
   CLUB_HOSTS='localhost;127.0.0.1;genealogy.example.com'
   ```

   Replace `genealogy.example.com` with your hostname. Enter just the hostname, without `https://` or a trailing slash. For the initial deployment, set `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD`. Single quotes preserve literal dollar signs in Compose's .env syntax.
3. Run the following commands. Compose binds the application to the server’s loopback interface.

   ```bash
   docker compose config --quiet
   docker compose up -d --build
   docker compose ps
   curl -fsS http://127.0.0.1:5186/ -o /dev/null
   ```

4. Copy the Nginx template into a new site configuration. Replace `genealogy.example.com` with your chosen hostname and change the proxy port if needed.

   ```bash
   sudo cp deploy/nginx/genealogy.conf /etc/nginx/sites-available/genealogy
   sudoedit /etc/nginx/sites-available/genealogy
   sudo ln -s /etc/nginx/sites-available/genealogy /etc/nginx/sites-enabled/genealogy
   sudo nginx -t
   sudo systemctl reload nginx
   ```

5. After DNS points to the server, configure HTTPS. With Certbot installed and no certificate for this hostname, run `sudo certbot --nginx -d genealogy.example.com --redirect` using your domain. Confirm the homepage loads over HTTPS and sign in as the administrator. Remove the two bootstrap variables from `.env` and run `docker compose up -d` to remove them from the container environment.

If you change the Docker bridge subnet, also update `PROXY_GATEWAY` to its gateway. The app trusts forwarded client addresses and protocol only from the configured proxy or loopback. Keep the cookie keys, database, and uploads in the persistent `club-data` volume.

## Update and back up

```bash
cd /opt/genealogy
git pull --ff-only
docker compose up -d --build
docker compose logs --tail=100 app
```

For a consistent backup, stop the app and copy the entire contents of `/data`, then restart it. Store the backup privately because it includes member accounts, private uploads, and sign-in keys.

```bash
umask 077
docker compose stop app
docker compose cp app:/data/. "../club-backup-$(date +%F-%H%M%S)"
docker compose start app
```

## Artwork

The homepage still life was generated with the built-in ImageGen tool and is stored at `wwwroot/images/family-archive.jpg`. Inter is distributed under the included SIL Open Font License.

<details>
<summary>ImageGen prompt</summary>

> Use case: photorealistic-natural. Asset type: landscape editorial photograph for the right half of a classy genealogy club website hero. Art-directed realistic overhead still life, late afternoon sunlight across an antique warm walnut desk. An open aged unlined cream genealogy ledger lies diagonally centered, with a finely hand-drawn branching family tree in faded brown ink on the two visible pages, hand-drawn empty ovals like a historic pedigree chart, subtle handwritten marks too small to read. Three authentic-looking sepia cabinet-card ancestral portraits overlap the upper right of the book. A dark brass magnifying glass rests partly over the right page near lower right. A single sprig of dried oak leaves on the top left. Real photography, quiet tactile depth, high quality restrained archival aesthetic, natural variation in the walnut and antique ivory paper, gold brown with rich soft shadow. The center and upper right contain the main subject with all elements recognizable. Medium close overhead framing, landscape 3:2. No legible words, no signage, no graphic borders, no watermark, no typography. This image occupies its own column, so fill the scene with meaningful detail.

</details>
