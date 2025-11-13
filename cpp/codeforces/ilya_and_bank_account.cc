#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int maximize(int64_t n) {
  if (n >= 0) return n;

  int last = abs(n % 10);
  int pre_last = abs((n / 10) % 10);

  if (last >= pre_last) {
    return n / 10;
  } else {
    return n / 10 + pre_last - last;
  }
}

int main(int argc, char *argv[]) {
  int64_t n;
  std::cin >> n;
  std::cout << maximize(n) << "\n";

  return 0;
}
