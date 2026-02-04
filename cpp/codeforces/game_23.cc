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
  int n, m;
  std::cin >> n >> m;

  if (m % n) {
    std::cout << -1 << "\n";
    return 0;
  }

  m = m / n;
  int res = 0;
  while (m != 1) {
    res++;

    if (m % 2 == 0) {
      m /= 2;
      continue;
    }

    if (m % 3 == 0) {
      m /= 3;
      continue;
    }

    break;
  }

  if (m != 1) {
    std::cout << -1 << "\n";
  } else {
    std::cout << res << "\n";
  }

  return 0;
}
