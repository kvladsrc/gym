#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int c = 0;
    int d = 0;
    std::cin >> n >> c >> d;

    map<int, int> m;
    for (int i = 0; i < (n * n); ++i) {
      int buf = 0;
      std::cin >> buf;
      m[buf]++;
    }

    auto first = (*m.begin()).first;
    bool res = true;
    for (int col = 0; col < n; ++col) {
      for (int row = 0; row < n; ++row) {
        auto need = first + (col * c) + (row * d);
        if (m[need] > 0) {
          m[need]--;
          continue;
        }

        res = false;
        break;
      }

      if (!res) {
        break;
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
