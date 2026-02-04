#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
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
    size_t n = 0;
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    size_t j = 1;
    bool found = false;
    for (; j < (n - 1); ++j) {
      if (a[j] > a[j - 1] && a[j] > a[j + 1]) {
        found = true;
        break;
      }
    }

    if (found) {
      std::cout << "YES" << "\n";
      j++;
      std::cout << j - 1 << " " << j << " " << j + 1 << "\n";
    } else {
      std::cout << "NO" << "\n";
    }
  }

  return 0;
}
