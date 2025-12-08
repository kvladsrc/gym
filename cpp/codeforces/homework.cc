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
    string a;
    std::cin >> a;
    int m = 0;
    std::cin >> m;
    string b;
    std::cin >> b;
    string c;
    std::cin >> c;
    string pref;

    size_t idx = 0;
    for (auto name : c) {
      if (name == 'V') {
        pref += b[idx++];
      } else {
        a += b[idx++];
      }
    }

    std::reverse(pref.begin(), pref.end());

    std::cout << pref + a << "\n";
  }

  return 0;
}
