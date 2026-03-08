FROM node:22-slim
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev
COPY src/ src/
COPY public/ public/
EXPOSE 8080
CMD ["node", "--experimental-transform-types", "src/index.ts"]
