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

int find_max(const string& a, const string& b, size_t b_idx) {
  int res = 0;
  for (size_t a_idx = 0; a_idx < a.size(); ++a_idx) {
    int local = 0;
    for (size_t i = 0; i + b_idx < b.size() && i + a_idx < a.size(); ++i) {
      if (a[a_idx + i] != b[b_idx + i]) {
        break;
      }
      local++;
    }
    res = max(res, local);
  }
  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    string a;
    string b;
    std::cin >> a >> b;

    int res = 0;
    for (size_t i = 0; i < b.size(); ++i) {
      res = max(res, find_max(a, b, i));
    }

    std::cout << a.size() + b.size() - (2 * res) << "\n";
  }

  return 0;
}
