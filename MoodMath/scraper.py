from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.chrome.service import Service
from webdriver_manager.chrome import ChromeDriverManager
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
import time
import urllib.parse

def scrape_reviews(url, progress_callback, wait_time=10.5):
    # Link düzenleme
    parsed = urllib.parse.urlparse(url)
    if not parsed.path.endswith("-yorumlari"):
        url = url.rstrip("/") + "-yorumlari"

    options = Options()
    options.add_argument('--headless')
    options.add_argument('--disable-gpu')
    options.add_argument('--no-sandbox')
    options.add_argument('--disable-dev-shm-usage')
    
    options.add_argument('user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36')
    
    driver = None
    try:
        progress_callback("Arka plan tarayıcısı başlatılıyor...")
        service = Service(ChromeDriverManager().install())
        driver = webdriver.Chrome(service=service, options=options)
        
        progress_callback("Hepsiburada sayfasına bağlanılıyor...")
        driver.get(url)
        
        progress_callback("Sayfa yüklendi, ürün ismi aranıyor...")
        
        # Ürün isminin bulunduğu sınıf
        product_name_element = WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.CLASS_NAME, "hermes-ProductRate-module-RUvPHodhCmmlEPTuTPsy"))
        )
        product_name = product_name_element.text.strip()
        if product_name.endswith("Değerlendirmeleri"):
            product_name = product_name.replace("Değerlendirmeleri", "").strip()

        progress_callback(f"Ürün bulundu: {product_name[:30]}... Yorum filtreleri aranıyor...")

        # Filtreleri bekle
        filters = WebDriverWait(driver, 10).until(
            EC.presence_of_all_elements_located((By.XPATH, "//*[@class='hermes-RateBox-module-wUSygDPCtThyMtSVappE hermes-RateBox-module-tEIJ6uc8H8YEp4UMCptW']"))
        )
        
        if len(filters) < 5:
            progress_callback("Uyarı: 5 yıldız filtresi tam bulunamadı. Bulunanlar üzerinden devam ediliyor...")

        all_reviews = []
        
        # filters 0'dan 4'e kadar (5 yıldızdan 1 yıldıza)
        for i in range(min(5, len(filters))):
            star_level = 5 - i
            progress_callback(f"{star_level} yıldızlı yorumlar çekiliyor...")
            
            # Filtreleri yeniden bulmak güvenlidir (DOM değişebilir)
            current_filters = driver.find_elements(By.XPATH, "//*[@class='hermes-RateBox-module-wUSygDPCtThyMtSVappE hermes-RateBox-module-tEIJ6uc8H8YEp4UMCptW']")
            if i < len(current_filters):
                # Tıkla ve bekle
                try:
                    driver.execute_script("arguments[0].click();", current_filters[i])
                    time.sleep(wait_time) # Yüklenmesi için zorunlu bekleme
                    
                    # Yorumları topla
                    review_cards = driver.find_elements(By.CLASS_NAME, "hermes-ReviewCard-module-KaU17BbDowCWcTZ9zzxw")
                    
                    added = 0
                    for card in review_cards:
                        if added >= 3:
                            break
                        rev_text = card.text.strip()
                        if rev_text:
                            all_reviews.append(rev_text)
                            added += 1
                            
                    # Filtreyi geri kaldır
                    current_filters = driver.find_elements(By.XPATH, "//*[@class='hermes-RateBox-module-wUSygDPCtThyMtSVappE hermes-RateBox-module-tEIJ6uc8H8YEp4UMCptW']")
                    driver.execute_script("arguments[0].click();", current_filters[i])
                    time.sleep(2)
                except Exception as inner_e:
                    progress_callback(f"{star_level} yıldızlı yorumlarda hata: {str(inner_e)[:30]}")
                    
        progress_callback(f"Toplam {len(all_reviews)} yorum başarıyla çekildi.")
        
        return {
            "product_name": product_name,
            "reviews": all_reviews
        }

    except Exception as e:
        raise Exception(f"Veri çekme işleminde hata oluştu: {str(e)}")
    finally:
        if driver:
            driver.quit()

