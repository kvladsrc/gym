#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;
    vector<int> a(n);
    map<int, int> m;
    for (auto &i : a) {
      std::cin >> i;
      m[i]++;
    }

    if (m.size() > 2) {
      std::cout << "NO" << "\n";
      continue;
    }

    if (m.size() == 1) {
      std::cout << "YES" << "\n";
      continue;
    }

    bool res = false;
    for (auto p : m) {
      if (p.second == static_cast<int>(n / 2)) {
        res = true;
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
