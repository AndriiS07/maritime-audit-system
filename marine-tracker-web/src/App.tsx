import { useEffect, useState } from 'react';
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import L from 'leaflet';
import { Navigation, Map as MapIcon, List, BarChart3, Ship } from 'lucide-react';
import { renderToStaticMarkup } from 'react-dom/server';
import './App.css';

interface Vessel {
  Mmsi: number;
  Name: string;
  VesselType: string;
  Latitude: number;
  Longitude: number;
  Course: number;
  Speed: number;
}

// Функція для створення векторної іконки, яка крутиться за курсом
const createCustomIcon = (type: string, course: number) => {
  const color = type === 'Cargo' ? '#f97316' : '#1e3a8a'; // Оранжевий для Cargo, темно-синій для Tanker
  
  const iconMarkup = renderToStaticMarkup(
    <div style={{ transform: `rotate(${course}deg)`, display: 'inline-block' }}>
      <Navigation fill={color} color={color} size={28} strokeWidth={1} />
    </div>
  );

  return L.divIcon({
    html: iconMarkup,
    className: 'custom-vessel-icon',
    iconSize: [28, 28],
    iconAnchor: [14, 14],
  });
};

function App() {
  const [vessels, setVessels] = useState<Vessel[]>([]);
  const [activeTab, setActiveTab] = useState<'map' | 'list' | 'stats'>('map');

  useEffect(() => {
    const fetchLiveVessels = async () => {
      try {
        const response = await fetch('http://localhost:5144/api/vessels/live');
        if (response.ok) {
          const data = await response.json();
          setVessels(data);
        }
      } catch (error) {
        console.error("Помилка завантаження даних:", error);
      }
    };

    fetchLiveVessels();
    const intervalId = setInterval(fetchLiveVessels, 3000);
    return () => clearInterval(intervalId);
  }, []);

  const totalCargo = vessels.filter(v => v.VesselType === 'Cargo').length;
  const totalTankers = vessels.filter(v => v.VesselType === 'Tanker').length;

  return (
    <div className="flex h-screen w-screen bg-slate-50 font-sans text-slate-800">
      
      {/* Лівий Сайдбар */}
      <div className="w-96 bg-white shadow-xl z-[1000] flex flex-col">
        <div className="p-6 bg-slate-900 text-white flex items-center gap-3">
          <Ship size={32} className="text-blue-400" />
          <h1 className="text-2xl font-bold tracking-wide">MarineTracker</h1>
        </div>

        {/* Навігація вкладок */}
        <div className="flex border-b">
          <button onClick={() => setActiveTab('map')} className={`flex-1 py-3 flex justify-center items-center gap-2 font-medium transition-colors ${activeTab === 'map' ? 'border-b-2 border-blue-600 text-blue-600' : 'text-slate-500 hover:text-slate-700'}`}>
            <MapIcon size={18} /> Карта
          </button>
          <button onClick={() => setActiveTab('list')} className={`flex-1 py-3 flex justify-center items-center gap-2 font-medium transition-colors ${activeTab === 'list' ? 'border-b-2 border-blue-600 text-blue-600' : 'text-slate-500 hover:text-slate-700'}`}>
            <List size={18} /> Список
          </button>
          <button onClick={() => setActiveTab('stats')} className={`flex-1 py-3 flex justify-center items-center gap-2 font-medium transition-colors ${activeTab === 'stats' ? 'border-b-2 border-blue-600 text-blue-600' : 'text-slate-500 hover:text-slate-700'}`}>
            <BarChart3 size={18} /> Статистика
          </button>
        </div>

        {/* Вміст вкладок */}
        <div className="flex-1 overflow-y-auto p-4">
          
          {activeTab === 'map' && (
            <div className="text-slate-600">
              <p className="mb-4">Спостереження за морським трафіком у реальному часі.</p>
              <div className="space-y-2 mt-4">
                <div className="flex items-center gap-2"><Navigation fill="#f97316" color="#f97316" size={20} /> <span className="font-medium">Cargo (Вантажні)</span></div>
                <div className="flex items-center gap-2"><Navigation fill="#1e3a8a" color="#1e3a8a" size={20} /> <span className="font-medium">Tanker (Танкери)</span></div>
              </div>
            </div>
          )}

          {activeTab === 'list' && (
            <div className="space-y-3">
              {vessels.map(v => (
                <div key={v.Mmsi} className="p-3 border rounded-lg hover:shadow-md transition-shadow bg-white">
                  <div className="font-bold text-slate-800">{v.Name !== "Unknown" ? v.Name : `MMSI: ${v.Mmsi}`}</div>
                  <div className="text-sm text-slate-500 flex justify-between mt-1">
                    <span className={v.VesselType === 'Cargo' ? 'text-orange-600 font-medium' : 'text-blue-800 font-medium'}>{v.VesselType}</span>
                    <span>{v.Speed || 0} вузлів</span>
                  </div>
                </div>
              ))}
            </div>
          )}

          {activeTab === 'stats' && (
            <div className="space-y-4">
              <div className="bg-slate-100 p-4 rounded-lg text-center">
                <div className="text-4xl font-bold text-slate-800">{vessels.length}</div>
                <div className="text-sm text-slate-500 uppercase tracking-wide mt-1">Суден у регіоні</div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="bg-orange-50 border border-orange-100 p-4 rounded-lg text-center">
                  <div className="text-2xl font-bold text-orange-600">{totalCargo}</div>
                  <div className="text-xs text-orange-600 uppercase tracking-wide mt-1">Вантажних</div>
                </div>
                <div className="bg-blue-50 border border-blue-100 p-4 rounded-lg text-center">
                  <div className="text-2xl font-bold text-blue-800">{totalTankers}</div>
                  <div className="text-xs text-blue-800 uppercase tracking-wide mt-1">Танкерів</div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Права частина: Карта */}
      <div className="flex-1 relative">
        <MapContainer 
          center={[43.5, 30.5]} 
          zoom={6} 
          style={{ height: '100%', width: '100%', zIndex: 0 }}
        >
          <TileLayer
            attribution='&copy; OpenStreetMap contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          
          {vessels.map(v => (
            <Marker 
              key={v.Mmsi} 
              position={[v.Latitude, v.Longitude]} 
              icon={createCustomIcon(v.VesselType, v.Course || 0)}
            >
              <Popup>
                <div className="text-center">
                  <strong className="block text-lg mb-1">{v.Name !== "Unknown" ? v.Name : `MMSI: ${v.Mmsi}`}</strong>
                  <span className={`inline-block px-2 py-1 text-xs font-bold rounded ${v.VesselType === 'Cargo' ? 'bg-orange-100 text-orange-700' : 'bg-blue-100 text-blue-800'}`}>
                    {v.VesselType}
                  </span>
                  <div className="mt-2 text-sm text-slate-600">
                    Швидкість: <b>{v.Speed || 0}</b> вузлів<br/>
                    Курс: <b>{v.Course || 0}°</b>
                  </div>
                </div>
              </Popup>
            </Marker>
          ))}
        </MapContainer>
      </div>
    </div>
  );
}

export default App;