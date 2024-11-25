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
    int n, m;
    std::cin >> n >> m;

    string x, s;
    std::cin >> x >> s;

    int operations = 0;
    while (x.size() < static_cast<size_t>(m)) {
      x += x;
      operations++;
    }

    if (x.find(s) != string::npos) {
      std::cout << operations << "\n";
      continue;
    }

    operations++;
    x += x;

    if (x.find(s) != string::npos) {
      std::cout << operations << "\n";
      continue;
    }

    std::cout << -1 << "\n";
  }

  return 0;
}
