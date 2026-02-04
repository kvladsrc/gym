#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n, k;
  std::cin >> n >> k;
  int time_to_solve = 4 * 60 - k;
  int next_problem = 1;
  while ((time_to_solve - 5 * next_problem >= 0) && (next_problem <= n)) {
    time_to_solve -= 5 * next_problem;
    next_problem++;
  }
  std::cout << next_problem - 1 << "\n";

  return 0;
}
