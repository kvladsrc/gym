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

int64_t pairs(int64_t team_size) { return (team_size * (team_size - 1)) / 2; }

int main(int argc, char* argv[]) {
  int64_t n, m;
  std::cin >> n >> m;

  // Other teams contains 1 member.
  int64_t largest_team = n - m + 1;
  int64_t max_pairs = pairs(largest_team);

  int64_t average_team = n / m;
  int64_t num_average_teams = m - (n % m);
  int64_t rest_teams = n % m;
  int64_t min_pairs = pairs(average_team) * num_average_teams;
  min_pairs += pairs(average_team + 1) * rest_teams;

  std::cout << min_pairs << " ";
  std::cout << max_pairs << "\n";

  return 0;
}
