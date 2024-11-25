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

  int n;
  std::cin >> n;
  int64_t sum = 0;
  int mx = 0;
  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    mx = max(mx, buf);
    sum += buf;
  }

  if (mx > sum - mx || sum % 2) {
    std::cout << "NO" << "\n";
  } else {
    std::cout << "YES" << "\n";
  }

  return 0;
}
