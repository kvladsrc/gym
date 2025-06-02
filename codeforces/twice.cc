#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <numeric>

using std::map;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    map<int, int> m;
    for (int i = 0; i < n; ++i) {
      int buf = 0;
      std::cin >> buf;
      m[buf]++;
    }

    int res = std::accumulate(
        m.begin(), m.end(), 0,
        [](int sum, const auto& p) { return sum + p.second / 2; });

    std::cout << res << "\n";
  }

  return 0;
}
