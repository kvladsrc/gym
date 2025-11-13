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

int main(int argc, char *argv[]) {
  int n, m;
  std::cin >> n >> m;
  int days = 0;
  int days_to_new = m;
  while (n) {
    days++;
    n--;
    days_to_new--;
    if (!days_to_new) {
      n++;
      days_to_new = m;
    }
  }
  std::cout << days << "\n";

  return 0;
}
