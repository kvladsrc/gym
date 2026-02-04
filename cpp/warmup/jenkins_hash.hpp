#ifndef CPP_WARMUP_JENKINS_HASH_H_
#define CPP_WARMUP_JENKINS_HASH_H_

#include <cstdint>
#include <string>

namespace warmup {

uint32_t jenkins_one_at_a_time_hash(const std::string& key);

}  // namespace warmup

#endif  // CPP_WARMUP_JENKINS_HASH_H_
