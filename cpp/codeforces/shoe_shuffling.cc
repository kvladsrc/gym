#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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
    std::cin >> n;

    map<int, vector<int>> m;
    for (int i = 0; i < n; ++i) {
      int buf = 0;
      std::cin >> buf;
      m[buf].push_back(i);
    }

    bool possible = true;
    vector<int> res(n);

    for (auto& p : m) {
      if (p.second.size() == 1) {
        possible = false;
        break;
      }

      for (size_t i = 1; i < p.second.size(); ++i) {
        res[p.second[i]] = p.second[i - 1];
      }

      res[p.second.front()] = p.second.back();
    }

    if (possible) {
      for (auto i : res) {
        std::cout << i + 1 << " ";
      }
      std::cout << "\n";
    } else {
      std::cout << -1 << "\n";
    }
  }

  return 0;
}
