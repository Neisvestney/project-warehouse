package app.projectwarehouse.client;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;

import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

/**
 * Capacitor плагин для приёма данных от аппаратного сканера АТОЛ E3.
 *
 * Настройка на устройстве:
 *   Barcode Utility → Scan Setting → Data Receive Method → BROADCAST_EVENT
 *
 * Точные значения SCAN_ACTION и SCAN_DATA_KEY уточнить из E3 Scanner SDK
 * (скачать с fs.atol.ru, раздел SDK → E3 Scanner SDK).
 */
@CapacitorPlugin(name = "AtolScanner")
public class AtolScannerPlugin extends Plugin {

    private static final String SCAN_ACTION = "android.intent.action.DECODE_DATA";
    private static final String SCAN_DATA_KEY = "barcode_string";

    private BroadcastReceiver scanReceiver;

    @PluginMethod
    public void startListening(PluginCall call) {
        if (scanReceiver != null) {
            call.resolve();
            return;
        }
        scanReceiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                String barcode = intent.getStringExtra(SCAN_DATA_KEY);
                if (barcode != null && !barcode.isEmpty()) {
                    JSObject result = new JSObject();
                    result.put("barcode", barcode);
                    notifyListeners("scanResult", result);
                }
            }
        };
        IntentFilter filter = new IntentFilter(SCAN_ACTION);
        getContext().registerReceiver(scanReceiver, filter);
        call.resolve();
    }

    @PluginMethod
    public void stopListening(PluginCall call) {
        if (scanReceiver != null) {
            getContext().unregisterReceiver(scanReceiver);
            scanReceiver = null;
        }
        call.resolve();
    }

    @Override
    protected void handleOnDestroy() {
        if (scanReceiver != null) {
            getContext().unregisterReceiver(scanReceiver);
            scanReceiver = null;
        }
    }
}
