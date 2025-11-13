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
#include <utility>
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

void swap(int &a, int &b) {
  int buf = a;
  a = b;
  b = buf;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int a, b;
    std::cin >> a >> b;

    if (a > b) swap(a, b);
    int diff = b - a;
    int res = min(diff / 2, a);

    a -= res;
    b -= res * 3;
    res += min(a, b) / 2;
    std::cout << res << "\n";
  }

  return 0;
}
