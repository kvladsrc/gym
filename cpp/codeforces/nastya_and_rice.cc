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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, a, b, c, d;
    std::cin >> n >> a >> b >> c >> d;

    int min_gain = (a - b) * n;
    int max_gain = (a + b) * n;
    int min_all = c - d;
    int max_all = c + d;

    if (max_gain < min_all || min_gain > max_all) {
      std::cout << "NO"
                << "\n";
    } else {
      std::cout << "YES"
                << "\n";
    }
  }

  return 0;
}
