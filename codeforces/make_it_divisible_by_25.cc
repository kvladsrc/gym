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
    string s;
    std::cin >> s;

    int case1 = 0;
    auto s_cpy = s;
    while (!s_cpy.empty() && s_cpy.back() != '5') {
      case1++;
      s_cpy.pop_back();
    }

    if (!s_cpy.empty()) {
      s_cpy.pop_back();
    }

    while (!s_cpy.empty() && (((s_cpy.back() - '0') * 10) + 5) % 25 != 0) {
      case1++;
      s_cpy.pop_back();
    }

    int case2 = 0;
    s_cpy = s;
    while (!s_cpy.empty() && s_cpy.back() != '0') {
      case2++;
      s_cpy.pop_back();
    }

    if (!s_cpy.empty()) {
      s_cpy.pop_back();
    }

    while (!s_cpy.empty() && ((s_cpy.back() - '0') * 10) % 25 != 0) {
      case2++;
      s_cpy.pop_back();
    }

    std::cout << min(case1, case2) << "\n";
  }

  return 0;
}
