FROM apache/superset:4.1.1@sha256:75bd685229d55d9709103a5d5994b798ca9d5b314a3b994ef4b9d7f6e198282b
USER root
RUN pip install --no-cache-dir psycopg2-binary==2.9.10
USER superset
