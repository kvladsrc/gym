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

int *minamal(int *a, int *b, int *c) {
  if (*a <= *b && *a <= *c)
    return a;

  if (*b <= *a && *b <= *c)
    return b;

  return c;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int a, b, c;
    std::cin >> a >> b >> c;

    for (int i = 0; i < 5; ++i) {
      auto to_increase = minamal(&a, &b, &c);
      (*to_increase)++;
    }

    std::cout << a * b * c << "\n";
  }

  return 0;
}
