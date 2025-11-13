#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t n, f, a, b;
    std::cin >> n >> f >> a >> b;

    int64_t moment = 0;
    bool res = true;
    while (n--) {
      int64_t m;
      std::cin >> m;

      if (!res) continue;

      int64_t charge = min((m - moment) * a, b);
      if (f <= charge) {
        res = false;
      } else {
        f -= charge;
      }
      moment = m;
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
