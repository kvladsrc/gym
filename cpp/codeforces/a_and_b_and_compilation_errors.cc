#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  int total = 0;

  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    total += buf;
  }

  int first_error = total;
  for (int i = 0; i < n - 1; ++i) {
    int buf;
    std::cin >> buf;
    first_error -= buf;
  }

  int second_error = total - first_error;
  for (int i = 0; i < n - 2; ++i) {
    int buf;
    std::cin >> buf;
    second_error -= buf;
  }

  std::cout << first_error << "\n";
  std::cout << second_error << "\n";

  return 0;
}
