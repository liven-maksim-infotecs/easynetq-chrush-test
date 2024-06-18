# easynetq-chrush-test

To run application change EasyTests__MustCrush env-variable in `docker-compose.yml` to desired value (true or false) and deploy with `docker compose up -d`.

True means that application will crush on startup.
False means that application will start successfully and than you can use the app to send some text via rmq. Use swagger for that purpose.
