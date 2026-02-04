#include <algorithm>
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

int sum(int n) {
  int res = 1, power = 1;
  while (n > 1) {
    res += power * 2;
    power *= 2;
    n--;
  }
  return res;
}

int first_day(int n) {
  int days = 2;
  while (true) {
    int c = sum(days);

    if (n % c == 0) {
      return n / c;
    }
    days++;
  }
  return -1;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    std::cout << first_day(n) << "\n";
  }
  return 0;
}
