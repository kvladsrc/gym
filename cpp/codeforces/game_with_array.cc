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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n, s;
  std::cin >> n >> s;

  if ((s / n) > 1) {
    std::cout << "YES\n";

    auto el = s / n;

    while (--n) {
      std::cout << el << " ";
      s -= el;
    }
    std::cout << s << "\n";
    std::cout << 1 << "\n";
  } else {
    std::cout << "NO" << "\n";
  }

  return 0;
}
